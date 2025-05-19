using System.Data.Common;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Outbox.Entities;

namespace Outbox.WebApi.EFCore;

public interface IOutboxMessageContext
{
    void AddM(OutboxMessage message);
    Task SaveChangesAsync(DbTransaction transaction, CancellationToken ct);
}

public class OutboxMessageContext : IOutboxMessageContext
{
    private readonly IOutboxMessagesProcessor _outboxMessagesProcessor;
    private readonly List<OutboxMessage> _messages = new();

    public OutboxMessageContext(IOutboxMessagesProcessor outboxMessagesProcessor) => 
        _outboxMessagesProcessor = outboxMessagesProcessor;

    public void AddM(OutboxMessage message)
    {
        _messages.Add(message);
    }

    public async Task SaveChangesAsync(DbTransaction transaction, CancellationToken ct)
    {
        if (!_messages.Any()) return;
        
        await using var batch = new NpgsqlBatch(transaction.Connection as NpgsqlConnection, transaction as NpgsqlTransaction);

        var lockCommand = batch.CreateBatchCommand();
        lockCommand.CommandText = "SELECT pg_advisory_xact_lock(554738281823524)";
        batch.BatchCommands.Add(lockCommand);
        
        foreach (var message in _messages)
        {
            var command = batch.CreateBatchCommand();
            command.CommandText = "insert into outbox.outbox_messages(topic, partition, type, key, payload, headers) values (@topic, @partition, @type, @key, @payload, @headers)";
            command.Parameters.AddWithValue("@topic", message.Topic);
            command.Parameters.AddWithValue("@partition", message.Partition);
            command.Parameters.AddWithValue("@type", message.Type);
            command.Parameters.AddWithValue("@key", message.Key);
            command.Parameters.AddWithValue("@payload", NpgsqlDbType.Jsonb, message.Payload);
            command.Parameters.AddWithValue("@headers", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(message.Headers));
            
            batch.BatchCommands.Add(command);
        }
        
        await batch.ExecuteNonQueryAsync(ct);
        
        _outboxMessagesProcessor.NewMessagesPersisted(_messages.First().Topic, _messages.First().Partition);
    }
}