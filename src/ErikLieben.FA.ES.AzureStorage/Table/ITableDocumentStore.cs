using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Extended interface for Table Storage-backed document stores that adds tag-based query support.
/// </summary>
public interface ITableDocumentStore : IDocumentStore
{
    /// <summary>
    /// Retrieves the first document matching the given document tag from the tag store.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name.</param>
    /// <param name="store">Optional store name for loading the document.</param>
    /// <returns>The first matching document or null if no document matches.</returns>
    Task<IObjectDocument?> GetFirstByDocumentByTagAsync(
        string objectName,
        string tag,
        string? documentTagStore = null,
        string? store = null);

    /// <summary>
    /// Retrieves all documents matching the given document tag from the tag store.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name.</param>
    /// <param name="store">Optional store name for loading the documents.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(
        string objectName,
        string tag,
        string? documentTagStore = null,
        string? store = null);
}
