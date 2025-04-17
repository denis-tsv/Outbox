namespace Outbox.Entities;

public class VirtualPartition
{
    public int Id { get; set; }
    public int Partition { get; set; }
    public string Topic { get; set; } = null!;
    public ulong LastProcessedTransactionId { get; set; }
    public int LastProcessedId { get; set; }
    public DateTimeOffset RetryAfter { get; set; }
}