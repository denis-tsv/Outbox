namespace Outbox.Entities;

public class OutboxOffsetSequence
{
    public int Id { get; set; }
    public string Topic { get; set; } = null!;
    public int Partition { get; set; }
    public int Value { get; set; }
}