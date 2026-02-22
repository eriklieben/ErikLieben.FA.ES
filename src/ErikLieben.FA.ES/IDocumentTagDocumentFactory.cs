using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

/// <summary>
/// Provides factory methods to create document and stream tag stores based on document context or explicit type.
/// </summary>
public interface IDocumentTagDocumentFactory
{
    /// <summary>
    /// Creates a document tag store using the default tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> instance using the default configuration.</returns>
    IDocumentTagStore CreateDocumentTagStore();

    /// <summary>
    /// Creates a document tag store using the tag configuration of the specified document.
    /// </summary>
    /// <param name="document">The object document whose tag configuration determines which store to create.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> instance appropriate for the document's configured tag type.</returns>
    IDocumentTagStore CreateDocumentTagStore(IObjectDocument document);

    /// <summary>
    /// Creates a document tag store for the specified tag provider type (for example, "blob").
    /// </summary>
    /// <param name="type">The tag provider type key used to resolve the underlying store implementation.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> instance for the requested provider type.</returns>
    IDocumentTagStore CreateDocumentTagStore(string type);

    /// <summary>
    /// Creates a stream tag store using the default stream tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> instance for stream tags.</returns>
    IDocumentTagStore CreateStreamTagStore();

    /// <summary>
    /// Creates a stream tag store using the stream tag configuration of the specified document.
    /// </summary>
    /// <param name="document">The object document whose stream tag configuration determines which store to create.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> instance for stream tags.</returns>
    IDocumentTagStore CreateStreamTagStore(IObjectDocument document);
}
