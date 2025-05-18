using System.Text;
using Confluent.Kafka;
using Medallion.Threading;
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
    private readonly IDistributedLockProvider _distributedLockProvider;

    //channel is faster then AutoResetEvent
    private readonly AutoResetEvent _autoResetEvent = new(false);
    
    public OutboxBackgroundService(
        IServiceProvider serviceProvider, 
        IOptions<OutboxConfiguration> outboxOptions,
        ILogger<OutboxBackgroundService> logger,
        IDistributedLockProvider distributedLockProvider)
    {
        _serviceProvider = serviceProvider;
        _outboxOptions = outboxOptions;
        _logger = logger;
        _distributedLockProvider = distributedLockProvider;
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

                if (processedMessages != _outboxOptions.Value.BatchSize)
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
        //pg_bouncer must be in session mode
        await using var advisoryLock = await _distributedLockProvider.AcquireLockAsync("Outbox", cancellationToken: cancellationToken);
        
        var offset = await dbContext.OutboxOffsets
            .FirstAsync(cancellationToken);
        
        var outboxMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Id > offset.LastProcessedId)
            .OrderBy(x => x.Id)
            .Take(_outboxOptions.Value.BatchSize)
            .ToArrayAsync(cancellationToken);

        if (!outboxMessages.Any()) return 0;

        await ProcessOutboxMessagesAsync(outboxMessages, cancellationToken);

        offset.LastProcessedId = outboxMessages.Last().Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        
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


    public void NewMessagesPersisted() => _autoResetEvent.Set();

    private async ValueTask WaitForOutboxMessage(CancellationToken stoppingToken)
    {
        _autoResetEvent.WaitOne(_outboxOptions.Value.NoMessagesDelay);
    }
}