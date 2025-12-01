using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Testing.InMemory;

/// <summary>
/// Provides an in-memory factory for creating document and stream tag stores for testing.
/// </summary>
public class InMemoryDocumentTagDocumentFactory : IDocumentTagDocumentFactory
{
    /// <summary>
    /// Creates a document tag store using the default configuration.
    /// </summary>
    /// <returns>An in-memory document tag store.</returns>
    public IDocumentTagStore CreateDocumentTagStore()
    {
        return new InMemoryDocumentTagStore();
    }

    /// <summary>
    /// Creates a document tag store for the specified document.
    /// </summary>
    /// <param name="document">The document whose tag configuration is used.</param>
    /// <returns>An in-memory document tag store.</returns>
    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);
        return new InMemoryDocumentTagStore();
    }

    /// <summary>
    /// Creates a document tag store for the specified type.
    /// </summary>
    /// <param name="type">The tag provider type.</param>
    /// <returns>An in-memory document tag store.</returns>
    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        return new InMemoryDocumentTagStore();
    }

    /// <summary>
    /// Creates a stream tag store using the default configuration.
    /// </summary>
    /// <returns>An in-memory stream tag store.</returns>
    public IDocumentTagStore CreateStreamTagStore()
    {
        return new InMemoryStreamTagStore();
    }

    /// <summary>
    /// Creates a stream tag store for the specified document.
    /// </summary>
    /// <param name="document">The document whose stream tag configuration is used.</param>
    /// <returns>An in-memory stream tag store.</returns>
    public IDocumentTagStore CreateStreamTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.EventStreamTagType);
        return new InMemoryStreamTagStore();
    }
}