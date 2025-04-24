namespace Outbox.Entities;

public class OutboxOffsetMessage
{
    public int Id { get; set; }
    public string Topic { get; set; } = null!;
    public int Partition { get; set; }
    public long Offset { get; set; }
    public string? Key { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public Dictionary<string, string> Headers { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}