namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Decorates an <see cref="ILeasedSession"/> to enable pre/post processing around stream actions.
/// </summary>
/// <remarks>
/// This implementation forwards calls to the underlying leased session while exposing hook points
/// (commented) for cross-cutting concerns such as logging or metrics.
/// </remarks>
/// <param name="session">The leased session to wrap. Must not be null.</param>
public class StreamActionLeasedSession(ILeasedSession session) : ILeasedSession
{

    /// <summary>
    /// Appends an event to the stream within the leased session.
    /// </summary>
    /// <typeparam name="PayloadType">The payload type of the event to append.</typeparam>
    /// <param name="payload">The event payload to persist.</param>
    /// <param name="actionMetadata">Optional metadata describing the action that produced the event.</param>
    /// <param name="overrideEventType">Optional event type name override; when null the type is inferred.</param>
    /// <param name="externalSequencer">Optional external sequencer identifier when sequencing is delegated.</param>
    /// <param name="metadata">Optional user-defined key/value metadata to attach to the event.</param>
    /// <returns>The appended event wrapper.</returns>
    public IEvent<PayloadType> Append<PayloadType>(PayloadType payload, ActionMetadata? actionMetadata = null, string? overrideEventType = null,
        string? externalSequencer = null, Dictionary<string, string>? metadata = null) where PayloadType : class
    {
        // Pre append

        return session.Append(payload, actionMetadata, overrideEventType, externalSequencer, metadata);

        // Post append
    }

    /// <summary>
    /// Gets the buffered events that have been prepared in the current leased session.
    /// </summary>
    public List<JsonEvent> Buffer => session.Buffer;

    /// <summary>
    /// Commits the buffered operations for the leased session.
    /// </summary>
    /// <returns>A task that completes when the commit has finished.</returns>
    public Task CommitAsync()
    {
        return session.CommitAsync();
    }

    /// <summary>
    /// Determines whether the specified stream is terminated.
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the stream.</param>
    /// <returns>A task that returns true when the stream is terminated; otherwise, false.</returns>
    public Task<bool> IsTerminatedASync(string streamIdentifier)
    {
        return session.IsTerminatedASync(streamIdentifier);
    }

    /// <summary>
    /// Reads events from the stream.
    /// </summary>
    /// <param name="startVersion">The start version to read from (inclusive). Defaults to 0.</param>
    /// <param name="untilVersion">The optional end version to read until (inclusive).</param>
    /// <returns>A task that returns the sequence of events, or null when no events are found.</returns>
    public Task<IEnumerable<IEvent>?> ReadAsync(int startVersion = 0, int? untilVersion = null)
    {
        return session.ReadAsync(startVersion, untilVersion);
    }
}
