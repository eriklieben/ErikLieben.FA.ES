namespace ErikLieben.FA.ES.Notifications;

public interface IStreamDocumentChunkClosedNotification : INotification
{
    Func<IEventStream, int, Task> StreamDocumentChunkClosed();
}