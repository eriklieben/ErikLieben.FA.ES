using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

/// <summary>
/// Defines operations to associate tags with object documents and query documents by tag.
/// </summary>
public interface IDocumentTagStore
{
    /// <summary>
    /// Associates the specified tag with the given document.
    /// </summary>
    /// <param name="document">The document to tag.</param>
    /// <param name="tag">The tag value to associate with the document.</param>
    /// <returns>A task that represents the asynchronous tagging operation.</returns>
    Task SetAsync(IObjectDocument document, string tag);

    /// <summary>
    /// Gets the identifiers of documents that have the specified tag within the given object scope.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of document identifiers that match; empty when none found.</returns>
    Task<IEnumerable<string>> GetAsync(string objectName, string tag);

    /// <summary>
    /// Removes the specified tag from the given document.
    /// </summary>
    /// <param name="document">The document to remove the tag from.</param>
    /// <param name="tag">The tag value to remove.</param>
    /// <returns>A task that represents the asynchronous removal operation.</returns>
    Task RemoveAsync(IObjectDocument document, string tag);
}
