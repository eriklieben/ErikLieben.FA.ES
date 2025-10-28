using System.Diagnostics.CodeAnalysis;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Defines operations for creating, retrieving, tagging, and saving object documents backed by Azure Blob Storage.
/// </summary>
public interface IBlobDocumentStore
{
    /// <summary>
    /// Creates a new object document with default stream metadata if it does not already exist.
    /// </summary>
    /// <param name="name">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The created or existing <see cref="IObjectDocument"/> instance.</returns>
    [return: MaybeNull]
    Task<IObjectDocument> CreateAsync(
        string name,
        string objectId,
        string? store = null);

    /// <summary>
    /// Retrieves an existing object document from storage.
    /// </summary>
    /// <param name="name">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The requested <see cref="IObjectDocument"/>.</returns>
    Task<IObjectDocument> GetAsync(
        string name,
        string objectId,
        string? store = null);

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

    /// <summary>
    /// Persists the specified object document to storage.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SetAsync(IObjectDocument document);
}
