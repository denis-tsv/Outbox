using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Context.Propagation;
using Outbox.Configurations;
using Outbox.Entities;
using Outbox.Extensions;
using Outbox.WebApi.Telemetry;

namespace Outbox.WebApi.BackgroundServices;

public class OneLongTransactionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OutboxConfiguration> _outboxOptions;
    private readonly ILogger<OneLongTransactionBackgroundService> _logger;

    public OneLongTransactionBackgroundService(
        IServiceProvider serviceProvider, 
        IOptions<OutboxConfiguration> outboxOptions,
        ILogger<OneLongTransactionBackgroundService> logger)
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
                } while (processedMessages > 0);

                await Task.Delay(_outboxOptions.Value.NoMessagesDelay, stoppingToken);
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

        var outboxMessages = await dbContext.OutboxMessages
            .Where(x => !x.Failed)
            .Where(x => x.RetryAfter == null || x.RetryAfter < DateTime.UtcNow)
            .OrderBy(x => x.CreatedAt)
            .ForUpdateSkipLocked()
            .Take(_outboxOptions.Value.BatchSize)
            .ToArrayAsync(cancellationToken);

        if (!outboxMessages.Any()) return 0;

        await ProcessOutboxMessagesAsync(dbContext, outboxMessages, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        
        await transaction.CommitAsync(cancellationToken);
        
        return outboxMessages.Length;
    }
    
    private Task ProcessOutboxMessagesAsync(AppDbContext dbContext, OutboxMessage[] outboxMessages, CancellationToken cancellationToken)
    {
        foreach (var message in outboxMessages)
        {
            var context = Propagators.DefaultTextMapPropagator.Extract(default, message.Headers, (d, s) => d.Where(x => x.Key == s).Select(x => x.Value).ToArray());
            using var activity = ActivitySources.Tracing.StartActivity("Outbox", ActivityKind.Internal, context.ActivityContext);

            try
            {
                //send

                dbContext.OutboxMessages.Remove(message);
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
}