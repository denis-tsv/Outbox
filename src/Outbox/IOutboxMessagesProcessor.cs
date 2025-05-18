namespace Outbox;

public interface IOutboxMessagesProcessor
{
    void NewMessagesPersisted();
}