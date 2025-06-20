using System.Text;
using Confluent.Kafka;
using LinqToDB;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Outbox.Configurations;
using Outbox.Entities;
using Outbox.WebApi.Linq2db;

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
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        
        var dataConnection = dbContext.CreateLinqToDBConnection();

        IQueryable<OutboxOffset> offsetsQuery = dataConnection.GetTable<OutboxOffset>();
        if (_offsetWithMessages != null)
        {
            offsetsQuery = offsetsQuery.Where(x => x.Topic == _offsetWithMessages.Value.Topic &&
                                                   x.Partition == _offsetWithMessages.Value.Partition);
            
            _offsetWithMessages = null; 
        }
        else
        {
            offsetsQuery = offsetsQuery.Where(x => DateTimeOffset.UtcNow > x.AvailableAfter)
                .OrderBy(x => x.AvailableAfter); //earliest available
        }

        var offsets = await offsetsQuery
            .Take(1)
            .SubQueryHint(PostgreSQLHints.ForUpdate)
            .SubQueryHint(PostgreSQLHints.SkipLocked)
            .AsSubQuery()
            .UpdateWithOutput(x => x,
                x => new OutboxOffset
                {
                    AvailableAfter = DateTimeOffset.UtcNow + _outboxOptions.Value.LockedDelay
                },
                (_, _, inserted) => inserted)
            .AsQueryable()
            .ToArrayAsyncLinqToDB(cancellationToken);
        
        if (!offsets.Any()) return -1; //no partition
        var offset = offsets.Single();
        
        var outboxMessages = await dataConnection.GetTable<OutboxMessage>()
            .Where(x => x.Topic == offset.Topic && 
                        x.Partition == offset.Partition &&
                        x.TransactionId >= offset.LastProcessedTransactionId &&
                        (
                            x.TransactionId > offset.LastProcessedTransactionId ||
                            x.TransactionId == offset.LastProcessedTransactionId && x.Id > offset.LastProcessedId
                        ) &&
                        x.TransactionId < PostgreSqlExtensions.MinCurrentTransactionId
            )
            .OrderBy(x => x.TransactionId).ThenBy(x => x.Id)
            .Take(_outboxOptions.Value.BatchSize)
            .ToArrayAsyncLinqToDB(cancellationToken);

        if (!outboxMessages.Any())
        {
            await dataConnection.GetTable<OutboxOffset>()
                .Where(x => x.Id == offset.Id)
                .Set(x => x.AvailableAfter, DateTimeOffset.UtcNow + _outboxOptions.Value.NoMessagesDelay)
                .UpdateAsync(cancellationToken);
            
            return 0;
        }

        await ProcessOutboxMessagesAsync(outboxMessages, cancellationToken);

        var lastMessage = outboxMessages.Last();
        await dataConnection.GetTable<OutboxOffset>()
            .Where(x => x.Id == offset.Id)
            .Set(x => x.LastProcessedId, lastMessage.Id)
            .Set(x => x.LastProcessedTransactionId, lastMessage.TransactionId)
            .Set(x => x.AvailableAfter, DateTimeOffset.UtcNow)
            .UpdateAsync(cancellationToken);
        
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