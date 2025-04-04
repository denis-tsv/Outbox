namespace Outbox;

public class OutboxMessage
{
    public int Id { get; set; }
    public ulong TransactionId { get; set; }
    public string Topic { get; set; } = null!;
    public int Partition { get; set; }
    public string? Key { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public Dictionary<string, string> Headers { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}