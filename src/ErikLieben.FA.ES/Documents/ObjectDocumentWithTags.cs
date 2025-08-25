namespace ErikLieben.FA.ES.Documents;

public class ObjectDocumentWithTags : ObjectDocument, IObjectDocumentWithMethods
{
    private readonly IDocumentTagStore documentTagStore;

    public ObjectDocumentWithTags(
        IObjectDocument document,
        IDocumentTagStore documentTagStore) : base(
    document?.ObjectId ?? throw new ArgumentNullException(nameof(document)),
    document?.ObjectName ?? throw new ArgumentNullException(nameof(document)),
    document?.Active ?? throw new ArgumentNullException(nameof(document)),
    document?.TerminatedStreams ?? throw new ArgumentNullException(nameof(document)),
    document?.SchemaVersion,
    document?.Hash,
    document?.PrevHash)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(documentTagStore);

        this.documentTagStore = documentTagStore;
    }


    public async Task SetTagAsync(string tag, TagTypes tagType = TagTypes.DocumentTag)
    {
        if (tagType == TagTypes.DocumentTag)
        {
            await documentTagStore.SetAsync(this, tag);
        }

        if (tagType == TagTypes.StreamTag)
        {
            throw new NotImplementedException("Not supported yet");
        }
    }
}
