using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Provides a simple wrapper around an <see cref="IDataStore"/> to facilitate stream actions.
/// </summary>
/// <param name="datastore">The underlying data store that performs the actual persistence and retrieval.</param>
public class StreamActionDataStore(IDataStore datastore) : IDataStore, IDataStoreRecovery
{
    /// <summary>
    /// Appends the specified events to the event stream for the given document by delegating to the underlying store.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events)
    {
        return datastore.AppendAsync(document, cancellationToken, events);
    }

    /// <summary>
    /// Appends the specified events to the event stream for the given document, optionally preserving timestamps.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp from events (useful for migrations).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events)
    {
        return datastore.AppendAsync(document, preserveTimestamp, cancellationToken, events);
    }

    /// <summary>
    /// Reads events for the specified document by delegating to the underlying store.
    /// </summary>
    /// <param name="document">The document whose events are read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier when chunking is enabled; null otherwise.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A sequence of events ordered by version, or null when the stream does not exist.</returns>
    public Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null, CancellationToken cancellationToken = default)
    {
        return datastore.ReadAsync(document, startVersion, untilVersion, chunk, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<IEvent> ReadAsStreamAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        return datastore.ReadAsStreamAsync(document, startVersion, untilVersion, chunk, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
        => ((IDataStoreRecovery)datastore).RemoveEventsForFailedCommitAsync(document, fromVersion, toVersion);
}
