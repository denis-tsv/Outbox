using System.Diagnostics;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Context.Propagation;
using Outbox.Configurations;
using Outbox.Entities;
using Outbox.WebApi.Telemetry;

namespace Outbox.WebApi.BackgroundServices;

public class OutboxOffsetBackgroundService : BackgroundService
{
    private static readonly BulkCopyOptions BulkCopyOptions = new()
    {
        BulkCopyType = BulkCopyType.ProviderSpecific,
        KeepIdentity = true
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxConfiguration> _outboxOptions;
    
    public OutboxOffsetBackgroundService(IServiceProvider serviceProvider, IOptions<OutboxConfiguration> outboxOptions)
    {
        _serviceProvider = serviceProvider;
        _outboxOptions = outboxOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            //disable tracking of outbox messages
            dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            var dataContext = dbContext.CreateLinqToDBContext();

            int processedMessages;
            do
            {
                processedMessages = await ProcessMessagesAsync(dbContext, dataContext, stoppingToken);
            } while (processedMessages >= 0);
            
            await Task.Delay(_outboxOptions.Value.NoMessagesDelay, stoppingToken);
        }
    }

    private async Task<int> ProcessMessagesAsync(AppDbContext dbContext, IDataContext dataContext, CancellationToken cancellationToken)
    {
        var partitions = await dataContext.GetTable<VirtualPartition>()
            .Where(x => x.Topic == "offset" && x.RetryAfter < DateTimeOffset.UtcNow)
            .OrderBy(x => x.RetryAfter)
            .Take(1)
            .SubQueryHint(PostgreSQLHints.ForUpdate)
            .SubQueryHint(PostgreSQLHints.SkipLocked)
            .AsSubQuery()
            .UpdateWithOutput(x => x,
                x => new VirtualPartition
                {
                    RetryAfter = DateTimeOffset.UtcNow + _outboxOptions.Value.LockedDelay
                },
                (_, _, inserted) => inserted)
            .AsQueryable()
            .ToArrayAsyncLinqToDB(cancellationToken);
        
        var partition = partitions.FirstOrDefault();
        if (partition == null) return -1;
        
        var outboxMessages = await dataContext.GetTable<OutboxOffsetMessage>()
            .Where(x => x.Topic == partition.Topic && 
                        x.Partition == partition.Partition &&
                        x.Offset > partition.LastProcessedId) //TODO rename to LastProcessedOffset in branch
            .OrderBy(x => x.Offset)
            .Take(_outboxOptions.Value.BatchSize)
            .ToArrayAsyncLinqToDB(cancellationToken);
        
        if (!outboxMessages.Any())
        {
            await dataContext.GetTable<VirtualPartition>()
                .Where(x => x.Id == partition.Id)
                .Set(x => x.RetryAfter, DateTimeOffset.UtcNow + _outboxOptions.Value.NoMessagesDelay)
                .UpdateAsync(cancellationToken);
            
            return 0;
        }
        
        await ProcessOutboxMessagesAsync(dataContext, outboxMessages, cancellationToken);
        
        var lastMessage = outboxMessages.Last();
        await dataContext.GetTable<VirtualPartition>()
            .Where(x => x.Id == partition.Id)
            .Set(x => x.LastProcessedId, lastMessage.Offset)
            .Set(x => x.RetryAfter, DateTimeOffset.UtcNow)
            .UpdateAsync(cancellationToken);
        
        return outboxMessages.Length;
    }

    private async Task ProcessOutboxMessagesAsync(IDataContext dataContext, OutboxOffsetMessage[] outboxMessages, CancellationToken cancellationToken)
    {
        var failedMessages = new List<FailedOutboxMessage>();
        
        foreach (var message in outboxMessages)
        {
            var context = Propagators.DefaultTextMapPropagator.Extract(default, message.Headers, (d, s) => d.Where(x => x.Key == s).Select(x => x.Value).ToArray());
            using var activity = ActivitySources.Tracing.StartActivity("Outbox", ActivityKind.Internal, context.ActivityContext);

            try
            {
                //Send message to broker like Kafka or RabbitMQ, or send HTTP/gRPC request to some external system...
                
            }
            catch (Exception e)
            {
                
            }
        }

        if (failedMessages.Any())
        {
            await dataContext.GetTable<FailedOutboxMessage>()
                .BulkCopyAsync(BulkCopyOptions, failedMessages, cancellationToken);
        }
    }
}
