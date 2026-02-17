using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob;

/// <summary>
/// Defines operations for creating, retrieving, tagging, and saving object documents backed by Azure Append Blob Storage.
/// Extends the base <see cref="IDocumentStore"/> interface with Azure Append Blob specific tag operations.
/// </summary>
public interface IAppendBlobDocumentStore : IDocumentStore
{
    /// <summary>
    /// Gets the first document that is associated with the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="tag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the document. If not provided, uses the default document store.</param>
    /// <returns>The first matching document or null when no document is found.</returns>
    Task<IObjectDocument?> GetFirstByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null);

    /// <summary>
    /// Gets all documents that are associated with the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="tag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the documents. If not provided, uses the default document store.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null);
}
