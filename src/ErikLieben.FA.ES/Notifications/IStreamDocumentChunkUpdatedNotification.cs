namespace ErikLieben.FA.ES.Notifications;

/// <summary>
/// Notification handler invoked when a stream document chunk is updated.
/// </summary>
public interface IStreamDocumentChunkUpdatedNotification : INotification
{
    /// <summary>
    /// Returns an action that handles stream document chunk updates.
    /// </summary>
    /// <returns>An action to execute when a chunk is updated.</returns>
    Action StreamDocumentChunkUpdated();
}
