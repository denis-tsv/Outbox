namespace Outbox.Configurations;

public class OutboxConfiguration
{
    public TimeSpan LockedDelay { get; set; }
    public TimeSpan NoMessagesDelay { get; set; }
    public int BatchSize { get; set; }
}