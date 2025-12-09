using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Defines the contract for an event store that persists and retrieves events for object documents.
/// </summary>
public interface IDataStore
{
    /// <summary>
    /// Appends the specified events to the event stream for the given document.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    Task AppendAsync(IObjectDocument document, params IEvent[] events);

    /// <summary>
    /// Appends the specified events to the event stream for the given document, optionally preserving original timestamps.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp from events (useful for migrations). Default is false.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    Task AppendAsync(IObjectDocument document, bool preserveTimestamp, params IEvent[] events);

    /// <summary>
    /// Reads events for the specified document.
    /// </summary>
    /// <param name="document">The document whose events are read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier when chunking is enabled; null otherwise.</param>
    /// <returns>A sequence of events ordered by version, or null when the stream does not exist.</returns>
    Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null,
        int? chunk = null);
}
