namespace ErikLieben.FA.ES;

/// <summary>
/// Represents a leased session for appending events to an event stream with transactional semantics.
/// </summary>
public interface ILeasedSession
{
    /// <summary>
    /// Appends an event with the specified payload to the session buffer.
    /// </summary>
    /// <typeparam name="PayloadType">The type of the event payload.</typeparam>
    /// <param name="payload">The event payload.</param>
    /// <param name="actionMetadata">Optional metadata about the action that triggered this event.</param>
    /// <param name="overrideEventType">Optional override for the event type name.</param>
    /// <param name="externalSequencer">Optional external sequencer identifier for event ordering.</param>
    /// <param name="metadata">Optional additional metadata as key-value pairs.</param>
    /// <returns>The created event.</returns>
    IEvent<PayloadType> Append<PayloadType>(
        PayloadType payload,
        ActionMetadata? actionMetadata = null,
        string? overrideEventType = null,
        string? externalSequencer = null,
        Dictionary<string, string>? metadata = null) where PayloadType : class;

    /// <summary>
    /// Gets the buffer of events pending commit in this session.
    /// </summary>
    List<JsonEvent> Buffer { get; }

    /// <summary>
    /// Commits all buffered events to the event stream.
    /// </summary>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    Task CommitAsync();

    /// <summary>
    /// Checks if a stream is terminated (has reached a terminal state).
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the stream to check.</param>
    /// <returns>True if the stream is terminated, otherwise false.</returns>
    Task<bool> IsTerminatedASync(string streamIdentifier);

    /// <summary>
    /// Reads events from the stream within the specified version range.
    /// </summary>
    /// <param name="startVersion">The starting version (inclusive). Defaults to 0.</param>
    /// <param name="untilVersion">The ending version (inclusive). If null, reads to the latest version.</param>
    /// <returns>The collection of events, or null if none found.</returns>
    Task<IEnumerable<IEvent>?> ReadAsync(int startVersion = 0, int? untilVersion = null);
}
