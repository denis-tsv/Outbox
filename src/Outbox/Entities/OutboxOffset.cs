namespace Outbox.Entities;

public class OutboxOffset
{
    public int Id { get; set; }
    public int LastProcessedId { get; set; }
}
