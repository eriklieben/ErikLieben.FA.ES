namespace ErikLieben.FA.ES.EventStreamManagement.Events;

/// <summary>
/// Event optionally appended after a successful rollback to provide audit trail in the event stream.
/// </summary>
/// <remarks>
/// <para>
/// This event is NOT automatically appended during cleanup. Use
/// <see cref="Repair.IStreamRepairService.AppendRollbackMarkerAsync"/> to explicitly add it
/// when you want the rollback recorded in the event stream itself.
/// </para>
/// <para>
/// WARNING: When appended, this advances the stream version. Future business events will start
/// at (marker version + 1), not at the original failed version. If you need retries to use
/// the same version numbers, do not append this marker.
/// </para>
/// </remarks>
public sealed record EventsRolledBackEvent
{
    /// <summary>
    /// The well-known event type name for rollback marker events.
    /// </summary>
    public const string EventTypeName = "EventStream.EventsRolledBack";

    /// <summary>
    /// Gets the first version that was rolled back.
    /// </summary>
    public required int FromVersion { get; init; }

    /// <summary>
    /// Gets the last version that was rolled back.
    /// </summary>
    public required int ToVersion { get; init; }

    /// <summary>
    /// Gets the number of events actually removed from storage.
    /// </summary>
    public required int EventsRemoved { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the rollback occurred.
    /// </summary>
    public required DateTimeOffset RolledBackAt { get; init; }

    /// <summary>
    /// Gets the reason for the rollback.
    /// </summary>
    public required RollbackReason Reason { get; init; }

    /// <summary>
    /// Gets the original error message if available.
    /// </summary>
    public string? OriginalErrorMessage { get; init; }

    /// <summary>
    /// Gets the type name of the original exception.
    /// </summary>
    public string? OriginalExceptionType { get; init; }

    /// <summary>
    /// Gets the correlation ID for tracing, if available.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Reasons for event rollback.
/// </summary>
public enum RollbackReason
{
    /// <summary>
    /// Partial commit failure with automatic cleanup.
    /// </summary>
    CommitFailure = 0,

    /// <summary>
    /// Manual repair of a broken stream.
    /// </summary>
    ManualRepair = 1,

    /// <summary>
    /// Migration or transformation operation.
    /// </summary>
    Migration = 2
}
