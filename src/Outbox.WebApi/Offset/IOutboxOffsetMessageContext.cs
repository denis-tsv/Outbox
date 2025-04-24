using System.Data.Common;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Outbox.Entities;

namespace Outbox.WebApi.Offset;

public interface IOutboxOffsetMessageContext
{
    void AddMessage(OutboxOffsetMessage message);
    Task SaveChangesAsync(DbTransaction transaction, CancellationToken ct);
}

public class OutboxOffsetMessageContext : IOutboxOffsetMessageContext
{
    private readonly List<OutboxOffsetMessage> _messages = new();
    
    public void AddMessage(OutboxOffsetMessage message)
    {
        _messages.Add(message);
    }

    public async Task SaveChangesAsync(DbTransaction transaction, CancellationToken ct)
    {
        await using var batch = new NpgsqlBatch(transaction.Connection as NpgsqlConnection, transaction as NpgsqlTransaction);

        foreach (var message in _messages)
        {
            var command = batch.CreateBatchCommand();
            command.CommandText = "call outbox.insert_outbox_offset_message(@topic, @partition, @type, @key, @payload, @headers)";
            command.Parameters.AddWithValue("@topic", message.Topic);
            command.Parameters.AddWithValue("@partition", message.Partition);
            command.Parameters.AddWithValue("@type", message.Type);
            command.Parameters.AddWithValue("@key", message.Key);
            command.Parameters.AddWithValue("@payload", NpgsqlDbType.Jsonb, message.Payload);
            command.Parameters.AddWithValue("@headers", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(message.Headers));
            
            batch.BatchCommands.Add(command);
        }

        await batch.ExecuteNonQueryAsync(ct);
    }
}