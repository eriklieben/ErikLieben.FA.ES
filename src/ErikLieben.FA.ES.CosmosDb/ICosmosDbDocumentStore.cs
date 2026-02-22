using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Extends <see cref="IDocumentStore"/> with CosmosDB-specific operations for document retrieval.
/// </summary>
public interface ICosmosDbDocumentStore : IDocumentStore
{
    /// <summary>
    /// Retrieves the first document matching the given document tag.
    /// </summary>
    /// <param name="objectName">The object name/type.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name override.</param>
    /// <param name="store">Optional store name override.</param>
    /// <returns>The first matching document, or null if none found.</returns>
    Task<IObjectDocument?> GetFirstByDocumentByTagAsync(
        string objectName,
        string tag,
        string? documentTagStore = null,
        string? store = null);

    /// <summary>
    /// Retrieves all documents matching the given document tag.
    /// </summary>
    /// <param name="objectName">The object name/type.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name override.</param>
    /// <param name="store">Optional store name override.</param>
    /// <returns>All matching documents.</returns>
    Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(
        string objectName,
        string tag,
        string? documentTagStore = null,
        string? store = null);
}
