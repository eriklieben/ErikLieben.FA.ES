namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Wraps an <see cref="IObjectDocument"/> to add tagging capabilities using an <see cref="IDocumentTagStore"/>.
/// </summary>
public class ObjectDocumentWithTags : ObjectDocument, IObjectDocumentWithMethods
{
    private readonly IDocumentTagStore documentTagStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectDocumentWithTags"/> class.
    /// </summary>
    /// <param name="document">The underlying document to enhance with tagging behavior.</param>
    /// <param name="documentTagStore">The tag store used to apply document tags.</param>
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

    /// <summary>
    /// Associates a tag with the document using the configured tag store.
    /// </summary>
    /// <param name="tag">The tag value to associate.</param>
    /// <param name="tagType">The tag category to apply; only <see cref="TagTypes.DocumentTag"/> is currently supported.</param>
    /// <exception cref="NotImplementedException">Thrown when <paramref name="tagType"/> is <see cref="TagTypes.StreamTag"/>.</exception>
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
