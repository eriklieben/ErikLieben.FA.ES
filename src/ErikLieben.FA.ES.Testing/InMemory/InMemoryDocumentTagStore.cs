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

        if (Tags.TryGetValue(document.ObjectId, out var list) && !list.Contains(tag))
        {
            list.Add(tag);
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
        if (!this.Tags.TryGetValue(objectName, out var value))
        {
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }
        return Task.FromResult<IEnumerable<string>>(value);
    }

    /// <summary>
    /// Removes the specified tag from the given document in memory.
    /// </summary>
    /// <param name="document">The document to remove the tag from.</param>
    /// <param name="tag">The tag value to remove.</param>
    /// <returns>A completed task.</returns>
    public Task RemoveAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        if (Tags.TryGetValue(document.ObjectId, out var list))
        {
            list.Remove(tag);
        }

        return Task.CompletedTask;
    }
}
