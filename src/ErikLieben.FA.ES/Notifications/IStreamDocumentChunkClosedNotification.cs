namespace ErikLieben.FA.ES.Notifications;

/// <summary>
/// Notification handler invoked when a stream document chunk is closed.
/// </summary>
public interface IStreamDocumentChunkClosedNotification : INotification
{
    /// <summary>
    /// Returns a function that handles stream document chunk closure.
    /// </summary>
    /// <returns>A function that takes an event stream and chunk identifier and performs the notification asynchronously.</returns>
    Func<IEventStream, int, Task> StreamDocumentChunkClosed();
}