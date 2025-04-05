using System.Diagnostics;
using LinqToDB;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Context.Propagation;
using Outbox.Configurations;
using Outbox.WebApi.Linq2db;
using Outbox.WebApi.Telemetry;

namespace Outbox.WebApi.BackgroundServices;

public class TwoShortTransactionsLinq2DbOutboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxConfiguration> _outboxOptions;
    
    public TwoShortTransactionsLinq2DbOutboxBackgroundService(IServiceProvider serviceProvider, IOptions<OutboxConfiguration> outboxOptions)
    {
        _serviceProvider = serviceProvider;
        _outboxOptions = outboxOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedMessages = await ProcessMessagesAsync(stoppingToken);
            if (processedMessages == 0) await Task.Delay(_outboxOptions.Value.NoMessagesDelay, stoppingToken);
        }
    }

    private async Task<int> ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        //disable tracking of outbox messages array
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        await using var dataContext = dbContext.CreateLinqToDBContext();
        
        // can be simplified after release of linq2db 6.0.0  https://github.com/linq2db/linq2db/issues/4414
        var ids = dataContext.GetTable<VirtualPartition>()
            .Where(x => x.RetryAt < DateTimeOffset.UtcNow)
            .OrderBy(x => x.RetryAt)
            .Take(1)
            .SubQueryHint(PostgreSQLHints.ForUpdate)
            .SubQueryHint(PostgreSQLHints.SkipLocked)
            .Select(x => x.Id);

        var partitions = await dataContext.GetTable<VirtualPartition>()
            .Where(x => ids.Contains(x.Id))
            .UpdateWithOutputAsync(
                dataContext.GetTable<VirtualPartition>(),
                x => new VirtualPartition
                {
                    RetryAt = DateTimeOffset.UtcNow + _outboxOptions.Value.LockedDelay
                },
                (source, deleted, inserted) => inserted, cancellationToken);

        var partition = partitions.FirstOrDefault();
        if (partition == null) return 0;
        
        var outboxMessages = await dataContext.GetTable<OutboxMessage>()
            .Where(x => x.Topic == partition.Topic && 
                        x.Partition == partition.Partition &&
                        x.TransactionId >= partition.LastProcessedTransactionId &&
                        x.Id > partition.LastProcessedId &&
                        x.TransactionId < PostgreSqlExtensions.MinCurrentTransactionId
            )
            .OrderBy(x => x.TransactionId).ThenBy(x => x.Id)
            .Take(_outboxOptions.Value.BatchSize)
            .ToArrayAsyncLinqToDB(cancellationToken);

        if (!outboxMessages.Any())
        {
            await dataContext.GetTable<VirtualPartition>()
                .Where(x => x.Id == partition.Id)
                .Set(x => x.RetryAt, DateTimeOffset.UtcNow + _outboxOptions.Value.NoMessagesDelay)
                .UpdateAsync(cancellationToken);
            
            return 0;
        }

        await ProcessOutboxMessagesAsync(outboxMessages, cancellationToken);

        var lastMessage = outboxMessages.Last();
        await dataContext.GetTable<VirtualPartition>()
            .Where(x => x.Id == partition.Id)
            .Set(x => x.LastProcessedId, lastMessage.Id)
            .Set(x => x.LastProcessedTransactionId, lastMessage.TransactionId)
            .Set(x => x.RetryAt, DateTimeOffset.UtcNow)
            .UpdateAsync(cancellationToken);

        return outboxMessages.Length;
    }

    private Task ProcessOutboxMessagesAsync(OutboxMessage[] outboxMessages, CancellationToken cancellationToken)
    {
        foreach (var message in outboxMessages)
        {
            var context = Propagators.DefaultTextMapPropagator.Extract(default, message.Headers, (d, s) => d.Where(x => x.Key == s).Select(x => x.Value).ToArray());
            using var activity = ActivitySources.Tracing.StartActivity("Outbox", ActivityKind.Internal, context.ActivityContext);
            
            //Send message to broker like Kafka or RabbitMQ, or send HTTP/gRPC request to some external system...
        }
        
        return Task.CompletedTask;
    }
}