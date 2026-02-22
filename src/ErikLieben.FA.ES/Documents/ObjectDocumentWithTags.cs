namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Wraps an <see cref="IObjectDocument"/> to add tagging capabilities using an <see cref="IDocumentTagStore"/>.
/// </summary>
public class ObjectDocumentWithTags : ObjectDocument, IObjectDocumentWithMethods
{
    private readonly IDocumentTagStore documentTagStore;
    private readonly IDocumentTagStore? streamTagStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectDocumentWithTags"/> class.
    /// </summary>
    /// <param name="document">The underlying document to enhance with tagging behavior.</param>
    /// <param name="documentTagStore">The tag store used to apply document tags.</param>
    public ObjectDocumentWithTags(
        IObjectDocument document,
        IDocumentTagStore documentTagStore) : this(document, documentTagStore, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectDocumentWithTags"/> class with both document and stream tag stores.
    /// </summary>
    /// <param name="document">The underlying document to enhance with tagging behavior.</param>
    /// <param name="documentTagStore">The tag store used to apply document tags.</param>
    /// <param name="streamTagStore">The tag store used to apply stream tags.</param>
    public ObjectDocumentWithTags(
        IObjectDocument document,
        IDocumentTagStore documentTagStore,
        IDocumentTagStore? streamTagStore) : base(
    (document ?? throw new ArgumentNullException(nameof(document))).ObjectId,
    document.ObjectName,
    document.Active,
    document.TerminatedStreams,
    document.SchemaVersion,
    document.Hash,
    document.PrevHash)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(documentTagStore);

        this.documentTagStore = documentTagStore;
        this.streamTagStore = streamTagStore;
    }

    /// <summary>
    /// Associates a tag with the document or stream using the configured tag store.
    /// </summary>
    /// <param name="tag">The tag value to associate.</param>
    /// <param name="tagType">The tag category to apply.</param>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="tagType"/> is <see cref="TagTypes.StreamTag"/> and no stream tag store is configured.</exception>
    public async Task SetTagAsync(string tag, TagTypes tagType = TagTypes.DocumentTag)
    {
        if (tagType == TagTypes.DocumentTag)
        {
            await documentTagStore.SetAsync(this, tag);
        }

        if (tagType == TagTypes.StreamTag)
        {
            if (streamTagStore == null)
            {
                throw new InvalidOperationException("Stream tag store is not configured. Ensure the event stream factory is configured with stream tag support.");
            }

            await streamTagStore.SetAsync(this, tag);
        }
    }

    /// <summary>
    /// Removes a tag from the document or stream using the configured tag store.
    /// </summary>
    /// <param name="tag">The tag value to remove.</param>
    /// <param name="tagType">The tag category to remove from.</param>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="tagType"/> is <see cref="TagTypes.StreamTag"/> and no stream tag store is configured.</exception>
    public async Task RemoveTagAsync(string tag, TagTypes tagType = TagTypes.DocumentTag)
    {
        if (tagType == TagTypes.DocumentTag)
        {
            await documentTagStore.RemoveAsync(this, tag);
        }

        if (tagType == TagTypes.StreamTag)
        {
            if (streamTagStore == null)
            {
                throw new InvalidOperationException("Stream tag store is not configured. Ensure the event stream factory is configured with stream tag support.");
            }

            await streamTagStore.RemoveAsync(this, tag);
        }
    }
}
