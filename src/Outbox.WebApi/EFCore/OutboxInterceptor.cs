using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Outbox.WebApi.EFCore;

public class OutboxInterceptor : ISaveChangesInterceptor
{
    private readonly IOutboxMessagesProcessor _outboxMessagesProcessor;

    public OutboxInterceptor(IOutboxMessagesProcessor outboxMessagesProcessor) => 
        _outboxMessagesProcessor = outboxMessagesProcessor;

    private readonly ConcurrentDictionary<DbContext, (string Topic, int Partition)> _contextsWithOutboxMessages = new();

    private void OnSavingChanges(DbContext context)
    {
        var message = context.ChangeTracker.Entries<OutboxMessage>()
            .FirstOrDefault(x => x.State == EntityState.Added);
        if (message != null) _contextsWithOutboxMessages.TryAdd(context, (message.Entity.Topic, message.Entity.Partition));
    }

    private void OnSaveChanges(DbContext context)
    {
        if (_contextsWithOutboxMessages.ContainsKey(context))
        {
            if (_contextsWithOutboxMessages.TryRemove(context, out var partition))
            {
                _outboxMessagesProcessor.NewMessagesPersisted(partition.Topic, partition.Partition);
            }
        }
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context != null)
            OnSavingChanges(eventData.Context);

        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context != null)
            OnSaveChanges(eventData.Context);

        return result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context != null)
            OnSaveChanges(eventData.Context);
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        if (eventData.Context != null)
            OnSavingChanges(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = new())
    {
        if (eventData.Context != null)
            OnSaveChanges(eventData.Context);

        return ValueTask.FromResult(result);
    }

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = new())
    {
        if (eventData.Context != null)
            OnSaveChanges(eventData.Context);

        return Task.CompletedTask;
    }
}