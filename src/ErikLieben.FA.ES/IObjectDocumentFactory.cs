using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

/// <summary>
/// Provides factory operations to retrieve, create, tag-query, and persist object documents.
/// </summary>
public interface IObjectDocumentFactory
{
    /// <summary>
    /// Retrieves an existing object document.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">An optional store type hint used to select an underlying provider; may be null.</param>
    /// <param name="documentType">An optional document type to override the default factory selection (e.g., "table", "blob"). Only used by composite factories.</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null!, string? documentType = null!);

    /// <summary>
    /// Retrieves an object document or creates a new one when it does not exist.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to retrieve or create.</param>
    /// <param name="store">An optional store type hint used to select an underlying provider; may be null.</param>
    /// <param name="documentType">An optional document type to override the default factory selection (e.g., "table", "blob"). Only used by composite factories.</param>
    /// <returns>The existing or newly created <see cref="IObjectDocument"/>.</returns>
    Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null!, string? documentType = null!);

    /// <summary>
    /// Gets the first object document that has the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the document. If not provided, uses the default document store.</param>
    /// <returns>The first matching document or null when none is found.</returns>
    Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null);

    /// <summary>
    /// Gets all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the documents. If not provided, uses the default document store.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null);

    /// <summary>
    /// Persists the provided object document to the underlying store.
    /// </summary>
    /// <param name="document">The object document to persist.</param>
    /// <param name="store">An optional store type hint used to select an underlying provider; may be null.</param>
    /// <param name="documentType">An optional document type to override the default factory selection (e.g., "table", "blob"). Only used by composite factories.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SetAsync(IObjectDocument document, string? store = null!, string? documentType = null!);
}
