using System.Text;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Outbox.Configurations;
using Outbox.Entities;

namespace Outbox.WebApi.BackgroundServices;

public class OutboxBackgroundService : BackgroundService, IOutboxMessagesProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxConfiguration> _outboxOptions;
    private readonly ILogger<OutboxBackgroundService> _logger;

    private readonly AutoResetEvent _autoResetEvent = new(false);

    private (string Topic, int Partition)? _offsetWithMessages;
    
    public OutboxBackgroundService(
        IServiceProvider serviceProvider, 
        IOptions<OutboxConfiguration> outboxOptions,
        ILogger<OutboxBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _outboxOptions = outboxOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                await using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var processedMessages = await ProcessMessagesAsync(dbContext, stoppingToken);

                if (processedMessages == -1) //no partition
                    await WaitForOutboxMessage(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Something wrong");
            }
        }
    }

    private async Task<int> ProcessMessagesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        //TODO Fix new messages persisted
        var offsets = await dbContext.OutboxOffsets
            .FromSql(
                $"""
                 UPDATE outbox.outbox_offsets
                 SET available_after = {DateTimeOffset.UtcNow + _outboxOptions.Value.LockedDelay}
                 FROM (
                     SELECT x.id as "Id"
                     FROM outbox.outbox_offsets x
                     WHERE {DateTimeOffset.UtcNow} > x.available_after
                     ORDER BY x.available_after
                     LIMIT 1
                     FOR UPDATE SKIP LOCKED
                 ) t1
                 WHERE outbox.outbox_offsets.id = t1."Id"
                 RETURNING outbox.outbox_offsets.*
                 """)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        
        if (!offsets.Any()) return -1; //no partition
        var offset = offsets.Single();

        var outboxMessages = await dbContext.OutboxMessages
            .FromSqlRaw(
                $"""
                SELECT *
                FROM outbox.outbox_messages x
                WHERE
                    x.topic = '{offset.Topic}' AND
                    x.partition = {offset.Partition} AND
                    x.transaction_id >= '{offset.LastProcessedTransactionId}'::xid8 AND
                    (x.transaction_id > '{offset.LastProcessedTransactionId}'::xid8 OR 
                     x.transaction_id = '{offset.LastProcessedTransactionId}'::xid8 AND x.id > {offset.LastProcessedId}) AND
                    x.transaction_id < pg_snapshot_xmin(pg_current_snapshot())
                ORDER BY x.transaction_id, x.id
                LIMIT {_outboxOptions.Value.BatchSize}
                """)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        if (!outboxMessages.Any())
        {
            await dbContext.OutboxOffsets
                .Where(x => x.Id == offset.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.AvailableAfter, DateTimeOffset.UtcNow + _outboxOptions.Value.NoMessagesDelay), cancellationToken);
                
            return 0;
        }

        await ProcessOutboxMessagesAsync(outboxMessages, cancellationToken);

        var lastMessage = outboxMessages.Last();
        await dbContext.OutboxOffsets
            .Where(x => x.Id == offset.Id)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.LastProcessedId, lastMessage.Id)
                .SetProperty(p => p.LastProcessedTransactionId, lastMessage.TransactionId)
                .SetProperty(p => p.AvailableAfter, DateTimeOffset.UtcNow),
                cancellationToken);
        
        return outboxMessages.Length;
    }
    
    private async Task ProcessOutboxMessagesAsync(OutboxMessage[] messages, CancellationToken cancellationToken)
    {
        var tasks = messages.Select(x =>
        {
            if (x.Key == null) return ProcessMessageAsync<Null>(null!, x, cancellationToken);
            return ProcessMessageAsync(x.Key!, x, cancellationToken);
        }).ToList();

        await Task.WhenAll(tasks);
    }
    
    private Task ProcessMessageAsync<TKey>(TKey key, OutboxMessage message, CancellationToken cancellationToken)
    {
        var kafkaMessage = new Message<TKey, string>
        {
            Key = key, 
            Value = message.Payload,
            Headers = new()
        };
        foreach (var header in message.Headers)
            kafkaMessage.Headers.Add(new Header(header.Key, Encoding.UTF8.GetBytes(header.Value)));    
        var producer = _serviceProvider.GetRequiredService<IProducer<TKey, string>>();
        return producer.ProduceAsync(message.Topic, kafkaMessage, cancellationToken);
    }


    public void NewMessagesPersisted(string topic, int partition)
    {
        _offsetWithMessages = (topic, partition);
        _autoResetEvent.Set();
    }

    private async ValueTask WaitForOutboxMessage(CancellationToken stoppingToken)
    {
        _autoResetEvent.WaitOne(_outboxOptions.Value.NoMessagesDelay);
    }
}