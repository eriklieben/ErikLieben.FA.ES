using TaskFlow.Domain.Messaging;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handler for updating a specific projection based on batched events
/// </summary>
public interface IProjectionHandler
{
    /// <summary>
    /// The name of the projection (for SignalR notifications)
    /// </summary>
    string ProjectionName { get; }

    /// <summary>
    /// Handles a batch of events, filtering for relevant ones and updating the projection
    /// </summary>
    Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the projection for SignalR notifications
    /// </summary>
    Task<ProjectionStatus> GetStatusAsync();
}

/// <summary>
/// Status information for a projection
/// </summary>
public record ProjectionStatus
{
    public required string Name { get; init; }
    public required int CheckpointCount { get; init; }
    public required string? CheckpointFingerprint { get; init; }
    public required DateTimeOffset? LastModified { get; init; }
    public required bool IsPersisted { get; init; }

    /// <summary>
    /// Duration of the last projection generation in milliseconds
    /// </summary>
    public long? LastGenerationDurationMs { get; init; }
}
