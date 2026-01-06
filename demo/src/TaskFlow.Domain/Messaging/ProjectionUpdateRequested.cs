using ErikLieben.FA.ES;

namespace TaskFlow.Domain.Messaging;

/// <summary>
/// Event published when projections need to be updated with new version tokens.
/// </summary>
public class ProjectionUpdateRequested : IProjectionEvent
{
    /// <summary>
    /// The version token for the updated aggregate.
    /// </summary>
    public required VersionToken VersionToken { get; init; }

    /// <summary>
    /// The object name (aggregate type) that was updated.
    /// </summary>
    public required string ObjectName { get; init; }

    /// <summary>
    /// The stream identifier.
    /// </summary>
    public required string StreamIdentifier { get; init; }

    /// <summary>
    /// Number of events in this update.
    /// </summary>
    public required int EventCount { get; init; }

    /// <summary>
    /// Timestamp of when the events were committed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional: specific projection names that should be updated.
    /// If null or empty, all applicable projections should be updated.
    /// </summary>
    public List<string>? TargetProjections { get; init; }
}
