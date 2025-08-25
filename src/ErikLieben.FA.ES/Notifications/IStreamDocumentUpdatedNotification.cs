namespace ErikLieben.FA.ES.Notifications;

public interface IStreamDocumentUpdatedNotification : INotification
{
    Action DocumentUpdated();
}
