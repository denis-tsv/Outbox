namespace Outbox.Entities;

public class FailedOutboxMessage
{
    public int Id { get; set; }
    public ulong TransactionId { get; set; }
    public string Topic { get; set; } = null!;
    public int Partition { get; set; }
    public string? Key { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public Dictionary<string, string> Headers { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public int RetryCount { get; set; }
    public bool Failed { get; set; }
    public DateTimeOffset? RetryAfter { get; set; }
}