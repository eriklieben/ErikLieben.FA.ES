using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Testing.InMemory;

/// <summary>
/// Provides an in-memory implementation of <see cref="IDocumentTagStore"/> for tests.
/// </summary>
public class InMemoryDocumentTagStore : IDocumentTagStore
{
    private readonly Dictionary<string, List<string>> Tags = new();

    /// <summary>
    /// Associates the specified tag with the given document in memory.
    /// </summary>
    /// <param name="document">The document to tag.</param>
    /// <param name="tag">The tag value to associate with the document.</param>
    /// <returns>A completed task.</returns>
    public Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        if (Tags.ContainsKey(document.ObjectId) && !Tags[document.ObjectId].Contains(tag))
        {
            Tags[document.ObjectId].Add(tag);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the identifiers of documents that have the specified tag within the given object scope.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of document identifiers that match; empty when none found.</returns>
    public Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        if (!this.Tags.ContainsKey(objectName))
        {
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }
        return Task.FromResult<IEnumerable<string>>(this.Tags[objectName]);
    }
}
