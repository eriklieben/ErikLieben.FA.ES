namespace ErikLieben.FA.ES.Notifications;

public interface IStreamDocumentChunkUpdatedNotification : INotification
{
    Action StreamDocumentChunkUpdated();
}
