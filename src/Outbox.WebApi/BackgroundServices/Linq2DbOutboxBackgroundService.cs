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

public class Linq2DbOutboxBackgroundService : BackgroundService, IOutboxMessagesProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxConfiguration> _outboxOptions;
    
    private readonly AutoResetEvent _event = new(false);
    private string? _topic;
    private int? _partition;
    
    public Linq2DbOutboxBackgroundService(IServiceProvider serviceProvider, IOptions<OutboxConfiguration> outboxOptions)
    {
        _serviceProvider = serviceProvider;
        _outboxOptions = outboxOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedMessages = await ProcessMessagesAsync(stoppingToken);
            if (processedMessages == 0) _event.WaitOne(_outboxOptions.Value.NoMessagesDelay);
        }
    }

    private async Task<int> ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        var partition = string.IsNullOrEmpty(_topic) && !_partition.HasValue
            ? await dbContext.VirtualPartitions.ToLinqToDB()
                .Where(x => x.RetryAt < DateTimeOffset.UtcNow)
                .OrderBy(x => x.RetryAt)
                .QueryHint(PostgreSQLHints.ForUpdate)
                .QueryHint(PostgreSQLHints.SkipLocked)
                .FirstOrDefaultAsyncLinqToDB(cancellationToken)
            : await dbContext.VirtualPartitions.ToLinqToDB()
                .Where(x => x.Topic == _topic && x.Partition == _partition)
                .QueryHint(PostgreSQLHints.ForUpdate)
                .QueryHint(PostgreSQLHints.SkipLocked)
                .FirstOrDefaultAsyncLinqToDB(cancellationToken);
        
        _topic = null; 
        _partition = null;
        
        if (partition == null) return 0;
        
        var outboxMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .ToLinqToDB()
            .Where(x => x.Topic == partition.Topic && 
                        x.Partition == partition.Partition &&
                        x.TransactionId >= partition.LastProcessedTransactionId &&
                        x.Id > partition.LastProcessedId &&
                        x.TransactionId < PostgreSqlExtensions.MinCurrentTransactionId
            )
            .OrderBy(x => x.TransactionId).ThenBy(x => x.Id)
            .Take(_outboxOptions.Value.BatchSize)
            .ToArrayAsyncEF(cancellationToken);

        if (!outboxMessages.Any())
        {
            partition.RetryAt = DateTimeOffset.UtcNow + _outboxOptions.Value.NoMessagesDelay;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        await ProcessOutboxMessagesAsync(outboxMessages, cancellationToken);

        var lastMessage = outboxMessages.Last();
        partition.LastProcessedId = lastMessage.Id;
        partition.LastProcessedTransactionId = lastMessage.TransactionId;
        partition.RetryAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        
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

    public void NewMessagesPersisted(string topic, int partition)
    {
        _topic = topic;
        _partition = partition;
        
        _event.Set();
    }
}