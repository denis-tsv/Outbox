using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using OpenTelemetry.Context.Propagation;
using Outbox.Configurations;
using Outbox.Extensions;
using Outbox.WebApi.Telemetry;

namespace Outbox.WebApi.BackgroundServices;

public class EFOutboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxConfiguration> _outboxOptions;

    public EFOutboxBackgroundService(IServiceProvider serviceProvider, IOptions<OutboxConfiguration> outboxOptions)
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        var partition = await dbContext.VirtualPartitions
            .Where(x => x.RetryAt < DateTimeOffset.UtcNow)
            .OrderBy(x => x.RetryAt)
            .ForUpdateSkipLocked()
            .FirstOrDefaultAsync(cancellationToken);
        if (partition == null) return 0;

        var transactionId = new NpgsqlParameter("last_processed_transaction_id", NpgsqlDbType.Xid8);
        transactionId.Value = partition.LastProcessedTransactionId;
        var outboxMessages = await dbContext.OutboxMessages
            .FromSql($"""
                      SELECT * FROM outbox.outbox_messages o
                      WHERE o.topic = {partition.Topic} AND
                            o.partition = {partition.Partition} AND
                            o.transaction_id >= {transactionId} AND
                            o.id > {partition.LastProcessedId} AND
                            o.transaction_id < pg_snapshot_xmin(pg_current_snapshot())
                      """)
            .AsNoTracking()
            .OrderBy(x => x.TransactionId).ThenBy(x => x.Id)
            .Take(_outboxOptions.Value.BatchSize)
            .ToArrayAsync(cancellationToken);

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
}