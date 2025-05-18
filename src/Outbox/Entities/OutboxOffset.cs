namespace Outbox.Entities;

public class OutboxOffset
{
    public int Id { get; set; }
    public long LastProcessedId { get; set; }
}
