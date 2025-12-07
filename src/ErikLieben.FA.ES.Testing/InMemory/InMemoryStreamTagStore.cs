using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Testing.InMemory;

/// <summary>
/// Provides an in-memory implementation of <see cref="IDocumentTagStore"/> for stream tags in tests.
/// </summary>
public class InMemoryStreamTagStore : IDocumentTagStore
{
    private readonly Dictionary<string, Dictionary<string, List<string>>> TagsByObjectName = new();

    /// <summary>
    /// Associates the specified tag with the stream of the given document in memory.
    /// </summary>
    /// <param name="document">The document whose stream is tagged.</param>
    /// <param name="tag">The tag value to associate with the stream.</param>
    /// <returns>A completed task.</returns>
    public Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var objectName = document.ObjectName;
        var streamId = document.Active.StreamIdentifier;

        if (!TagsByObjectName.TryGetValue(objectName, out var streamTags))
        {
            streamTags = new Dictionary<string, List<string>>();
            TagsByObjectName[objectName] = streamTags;
        }

        if (!streamTags.TryGetValue(streamId, out var tags))
        {
            tags = new List<string>();
            streamTags[streamId] = tags;
        }

        if (!tags.Contains(tag))
        {
            tags.Add(tag);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the identifiers of streams that have the specified tag within the given object scope.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of stream identifiers that match; empty when none found.</returns>
    public Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        if (!TagsByObjectName.TryGetValue(objectName, out var streamTags))
        {
            return Task.FromResult<IEnumerable<string>>([]);
        }

        var matchingStreams = streamTags
            .Where(kvp => kvp.Value.Contains(tag))
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult<IEnumerable<string>>(matchingStreams);
    }
}
