using System.Runtime.CompilerServices;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Testing.InMemory;

/// <summary>
/// Provides an in-memory implementation of <see cref="IDataStore"/> intended for tests.
/// </summary>
public class InMemoryDataStore : IDataStore, IDataStoreRecovery
{
    /// <summary>
    /// Gets the internal storage of events grouped by stream identifier and version.
    /// </summary>
    public Dictionary<string, Dictionary<int, IEvent>> Store { get; } = new();

    /// <summary>
    /// Appends the specified events to the in-memory event stream for the given document.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order.</param>
    /// <returns>A completed task.</returns>
    public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events)
    {
        var identifier = GetStoreKey(document.ObjectName, document.ObjectId);
        foreach (var @event in events)
        {
            if (!Store.TryGetValue(identifier, out var dict))
            {
                dict = new();
                Store[identifier] = dict;
            }

            var count = dict.Count;
            dict[count] = @event;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Appends the specified events to the in-memory event stream for the given document.
    /// The preserveTimestamp parameter is ignored for in-memory storage as events are stored as-is.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="preserveTimestamp">Ignored for in-memory storage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order.</param>
    /// <returns>A completed task.</returns>
    public Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events)
    {
        // In-memory storage doesn't modify timestamps, so just delegate to the regular method
        return AppendAsync(document, cancellationToken, events);
    }

    /// <summary>
    /// Reads events for the specified document from in-memory storage.
    /// </summary>
    /// <param name="document">The document whose events are read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive). Ignored by this implementation.</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end. Ignored by this implementation.</param>
    /// <param name="chunk">The chunk identifier when chunking is enabled; null otherwise. Ignored by this implementation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A sequence of events ordered by version, or an empty sequence when the stream does not exist.</returns>
    public Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);

        var identifier = GetStoreKey(document.ObjectName, document.ObjectId);

        if (!Store.TryGetValue(identifier, out var dict))
        {
            return Task.FromResult<IEnumerable<IEvent>?>(new List<IEvent>());
        }

        var storedEvents = dict.Values.ToList();
        return Task.FromResult<IEnumerable<IEvent>?>(storedEvents);
    }

    /// <summary>
    /// Reads events for the specified document as a streaming async enumerable.
    /// </summary>
    /// <param name="document">The document whose events are read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier when chunking is enabled; null otherwise.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the streaming operation.</param>
    /// <returns>An async enumerable of events ordered by version.</returns>
    public IAsyncEnumerable<IEvent> ReadAsStreamAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);
        return ReadAsStreamAsyncCore(document, startVersion, untilVersion, chunk, cancellationToken);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private async IAsyncEnumerable<IEvent> ReadAsStreamAsyncCore(
        IObjectDocument document,
        int startVersion,
        int? untilVersion,
        int? chunk,
        [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore CS1998
    {
        var identifier = GetStoreKey(document.ObjectName, document.ObjectId);

        if (!Store.TryGetValue(identifier, out var dict))
        {
            yield break;
        }

        foreach (var evt in dict.Values
            .Where(e => e.EventVersion >= startVersion && (!untilVersion.HasValue || e.EventVersion <= untilVersion))
            .OrderBy(e => e.EventVersion))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
        }
    }

    /// <summary>
    /// Gets the backing dictionary for a specific stream id.
    /// </summary>
    /// <param name="storeId">The stream identifier.</param>
    /// <returns>A dictionary mapping version to event for the specified stream.</returns>
    public Dictionary<int, IEvent> GetDataStoreFor(string storeId)
    {
        return Store[storeId];
    }

    /// <summary>
    /// Removes the specified events from the in-memory store for the given document.
    /// </summary>
    /// <param name="document">The document whose events are removed.</param>
    /// <param name="events">The events to remove.</param>
    /// <returns>A completed task.</returns>
    public Task RemoveAsync(IObjectDocument document, params IEvent[] events)
    {
        var identifier = GetStoreKey(document.ObjectName, document.ObjectId);
        if (Store.TryGetValue(identifier, out var dict))
        {
            var keysToRemove = events
                .Where(e => dict.ContainsKey(e.EventVersion))
                .Select(e => e.EventVersion)
                .ToList();

            foreach (var key in keysToRemove)
            {
                dict.Remove(key);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
    {
        var identifier = GetStoreKey(document.ObjectName, document.ObjectId);
        if (!Store.TryGetValue(identifier, out var dict))
        {
            return Task.FromResult(0);
        }

        var removed = 0;
        for (var version = fromVersion; version <= toVersion; version++)
        {
            if (dict.Remove(version))
            {
                removed++;
            }
        }

        return Task.FromResult(removed);
    }

    /// <summary>
    /// Creates the canonical in-memory key used to store events for the specified object.
    /// </summary>
    /// <param name="objectName">The name of the object.</param>
    /// <param name="objectId">The identifier of the object.</param>
    /// <returns>A normalized key in the form "lower(objectName)__objectId".</returns>
    public static string GetStoreKey(string objectName, string objectId)
    {
        return $"{objectName.ToLowerInvariant()}__{objectId}";
    }
}
