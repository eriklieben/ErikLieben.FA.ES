namespace ErikLieben.FA.ES.EventStreamManagement.Events;

/// <summary>
/// Event appended to close a stream and redirect to a continuation stream.
/// This event type is checked by all DataStore implementations before allowing appends.
/// When this event is the last event in a stream, the stream is considered closed.
/// </summary>
/// <remarks>
/// The event type name "EventStream.Closed" is used as a convention across all storage providers.
/// DataStore implementations check for this event type to prevent further appends.
/// </remarks>
public sealed record StreamClosedEvent
{
    /// <summary>
    /// The well-known event type name for stream closure events.
    /// </summary>
    public const string EventTypeName = "EventStream.Closed";

    /// <summary>
    /// Gets or sets the stream identifier where operations should continue.
    /// </summary>
    public required string ContinuationStreamId { get; init; }

    /// <summary>
    /// Gets or sets the type of the continuation stream (blob, table, cosmos).
    /// </summary>
    public required string ContinuationStreamType { get; init; }

    /// <summary>
    /// Gets or sets the data store connection name for the continuation stream.
    /// </summary>
    public required string ContinuationDataStore { get; init; }

    /// <summary>
    /// Gets or sets the document store connection name for the continuation stream.
    /// </summary>
    public required string ContinuationDocumentStore { get; init; }

    /// <summary>
    /// Gets or sets the reason for closing the stream.
    /// </summary>
    public required StreamClosureReason Reason { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the stream was closed.
    /// </summary>
    public required DateTimeOffset ClosedAt { get; init; }

    /// <summary>
    /// Gets or sets the optional migration ID if this closure is part of a migration.
    /// </summary>
    public string? MigrationId { get; init; }

    /// <summary>
    /// Gets or sets the version of the last business event before closure.
    /// This is the version at which the target stream should be synced.
    /// </summary>
    public int LastBusinessEventVersion { get; init; }

    /// <summary>
    /// Gets or sets optional metadata for the closure.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Reasons for closing an event stream.
/// </summary>
public enum StreamClosureReason
{
    /// <summary>
    /// Stream migrated to a new location.
    /// </summary>
    Migration = 0,

    /// <summary>
    /// Stream reached size limit and was split.
    /// </summary>
    SizeLimit = 1,

    /// <summary>
    /// Stream archived for retention compliance.
    /// </summary>
    Archival = 2,

    /// <summary>
    /// Manual closure by administrator.
    /// </summary>
    Manual = 3
}
