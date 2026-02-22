namespace ErikLieben.FA.ES.Results;

/// <summary>
/// Represents an error with a code and message.
/// </summary>
/// <param name="Code">The error code for programmatic error handling.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>
    /// An unknown error.
    /// </summary>
    public static readonly Error Unknown = new("UNKNOWN", "An unknown error occurred");

    /// <summary>
    /// A null value was provided where a non-null value was expected.
    /// </summary>
    public static readonly Error NullValue = new("NULL_VALUE", "A null value was provided");

    /// <summary>
    /// Creates an error from an exception.
    /// </summary>
    /// <param name="exception">The exception to create an error from.</param>
    public static Error FromException(Exception exception) =>
        new($"EXCEPTION.{exception.GetType().Name}", exception.Message);
}

/// <summary>
/// Contains common error codes used by the event sourcing library.
/// </summary>
public static class EventSourcingErrors
{
    /// <summary>
    /// The stream does not exist.
    /// </summary>
    public static Error StreamNotFound(string streamId) =>
        new("STREAM_NOT_FOUND", $"Event stream '{streamId}' was not found");

    /// <summary>
    /// A concurrency conflict occurred.
    /// </summary>
    public static Error ConcurrencyConflict(string streamId, int expectedVersion, int actualVersion) =>
        new("CONCURRENCY_CONFLICT", $"Concurrency conflict on stream '{streamId}'. Expected version {expectedVersion}, but actual version is {actualVersion}");

    /// <summary>
    /// The aggregate was not found.
    /// </summary>
    public static Error AggregateNotFound(string aggregateType, string id) =>
        new("AGGREGATE_NOT_FOUND", $"Aggregate '{aggregateType}' with id '{id}' was not found");

    /// <summary>
    /// The aggregate already exists.
    /// </summary>
    public static Error AggregateAlreadyExists(string aggregateType, string id) =>
        new("AGGREGATE_ALREADY_EXISTS", $"Aggregate '{aggregateType}' with id '{id}' already exists");

    /// <summary>
    /// Failed to deserialize an event.
    /// </summary>
    public static Error EventDeserializationFailed(string eventType) =>
        new("EVENT_DESERIALIZATION_FAILED", $"Failed to deserialize event of type '{eventType}'");

    /// <summary>
    /// The projection was not found.
    /// </summary>
    public static Error ProjectionNotFound(string projectionType, string id) =>
        new("PROJECTION_NOT_FOUND", $"Projection '{projectionType}' with id '{id}' was not found");

    /// <summary>
    /// Failed to save the projection.
    /// </summary>
    public static Error ProjectionSaveFailed(string projectionType, string message) =>
        new("PROJECTION_SAVE_FAILED", $"Failed to save projection '{projectionType}': {message}");

    /// <summary>
    /// The snapshot was not found.
    /// </summary>
    public static Error SnapshotNotFound(string streamId, int version) =>
        new("SNAPSHOT_NOT_FOUND", $"Snapshot for stream '{streamId}' at version {version} was not found");

    /// <summary>
    /// A storage operation failed.
    /// </summary>
    public static Error StorageOperationFailed(string operation, string message) =>
        new("STORAGE_OPERATION_FAILED", $"Storage operation '{operation}' failed: {message}");

    /// <summary>
    /// The operation was cancelled.
    /// </summary>
    public static readonly Error OperationCancelled =
        new("OPERATION_CANCELLED", "The operation was cancelled");

    /// <summary>
    /// The operation timed out.
    /// </summary>
    public static Error Timeout(TimeSpan duration) =>
        new("TIMEOUT", $"The operation timed out after {duration.TotalSeconds:F1} seconds");

    /// <summary>
    /// Validation failed.
    /// </summary>
    public static Error ValidationFailed(string message) =>
        new("VALIDATION_FAILED", message);
}
