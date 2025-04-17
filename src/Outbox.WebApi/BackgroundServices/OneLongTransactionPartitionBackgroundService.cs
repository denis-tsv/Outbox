using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using OpenTelemetry.Context.Propagation;
using Outbox.Configurations;
using Outbox.Entities;
using Outbox.Extensions;
using Outbox.WebApi.Telemetry;

namespace Outbox.WebApi.BackgroundServices;

public class OneLongTransactionPartitionBackgroundService : BackgroundService, IOutboxMessagesProcessor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxConfiguration> _outboxOptions;
    private readonly ILogger<OneLongTransactionPartitionBackgroundService> _logger;

    private readonly AutoResetEvent _event = new(false);
    private string? _topic;
    private int? _partition;

    public OneLongTransactionPartitionBackgroundService(
        IServiceProvider serviceProvider, 
        IOptions<OutboxConfiguration> outboxOptions,
        ILogger<OneLongTransactionPartitionBackgroundService> logger)
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
                int processedMessages;
                do
                {
                    processedMessages = await ProcessMessagesAsync(dbContext, stoppingToken);
                    dbContext.ChangeTracker.Clear();
                } while (processedMessages >= 0);

                _event.WaitOne(_outboxOptions.Value.NoMessagesDelay);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Something wrong");
            }
        }
    }

    private async Task<int> ProcessMessagesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var partition = string.IsNullOrEmpty(_topic) && !_partition.HasValue
            ? await dbContext.VirtualPartitions
                .Where(x => x.RetryAfter < DateTimeOffset.UtcNow)
                .OrderBy(x => x.RetryAfter)
                .ForUpdateSkipLocked()
                .FirstOrDefaultAsync(cancellationToken)
            : await dbContext.VirtualPartitions
                .Where(x => x.Topic == _topic && x.Partition == _partition)
                .ForUpdateSkipLocked()
                .FirstOrDefaultAsync(cancellationToken);
        
        _topic = null; 
        _partition = null;
        
        if (partition == null) return -1;

        var transactionId = new NpgsqlParameter("last_processed_transaction_id", NpgsqlDbType.Xid8);
        transactionId.Value = partition.LastProcessedTransactionId;
        var outboxMessages = await dbContext.OutboxMessages
            .FromSql($"""
                      SELECT * FROM outbox.outbox_messages o
                      WHERE o.topic = {partition.Topic} AND
                            o.partition = {partition.Partition} AND
                            o.transaction_id >= {transactionId} AND
                            o.id > {partition.LastProcessedId} AND
                            o.transaction_id < pg_snapshot_xmin(pg_current_snapshot()) AND
                            o.failed = false AND
                            (o.retry_after is null OR o.retry_after < {DateTime.UtcNow})  
                      """)
            .OrderBy(x => x.TransactionId).ThenBy(x => x.Id)
            .Take(_outboxOptions.Value.BatchSize)
            .ToArrayAsync(cancellationToken);

        if (!outboxMessages.Any())
        {
            partition.RetryAfter = DateTimeOffset.UtcNow + _outboxOptions.Value.NoMessagesDelay;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        await ProcessOutboxMessagesAsync(outboxMessages, cancellationToken);

        var lastMessage = outboxMessages.FirstOrDefault(x => x is {Failed: false, RetryAfter: not null}) 
                          ?? outboxMessages.Last();
        partition.LastProcessedId = lastMessage.Id;
        partition.LastProcessedTransactionId = lastMessage.TransactionId;
        partition.RetryAfter = DateTimeOffset.UtcNow;
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

            try
            {
                //send
                
            }
            catch (Exception e)
            {
                if (message.RetryCount >= 3)  //max retry count
                {
                    message.Failed = true;
                }
                else
                {
                    message.RetryCount++;
                    message.RetryAfter = DateTimeOffset.UtcNow.AddSeconds(1); // some strategy
                }
            }
            
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