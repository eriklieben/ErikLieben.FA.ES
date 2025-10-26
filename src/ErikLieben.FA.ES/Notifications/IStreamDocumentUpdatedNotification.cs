namespace ErikLieben.FA.ES.Notifications;

/// <summary>
/// Notification handler invoked when a stream document is updated.
/// </summary>
public interface IStreamDocumentUpdatedNotification : INotification
{
    /// <summary>
    /// Returns an action that handles stream document updates.
    /// </summary>
    /// <returns>An action to execute when the document is updated.</returns>
    Action DocumentUpdated();
}
