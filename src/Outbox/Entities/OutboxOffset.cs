namespace Outbox.Entities;

public class OutboxOffset
{
    public int Id { get; set; }
    public string Topic { get; set; } = null!;
    public int Partition { get; set; }
    public int LastProcessedId { get; set; }
    public DateTimeOffset AvailableAfter { get; set; }

}
