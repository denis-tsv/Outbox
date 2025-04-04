namespace Outbox;

public interface IOutboxMessagesProcessor
{
    void NewMessagesPersisted(string topic, int partition);
}