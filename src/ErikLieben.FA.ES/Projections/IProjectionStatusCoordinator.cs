namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Service to manage projection status transitions with proper coordination.
/// Handles setting status to Rebuilding before rebuild starts, and back to Active when complete.
/// </summary>
public interface IProjectionStatusCoordinator
{
    /// <summary>
    /// Starts a rebuild, setting status to Rebuilding.
    /// Returns a token that must be passed to Complete/Cancel.
    /// </summary>
    /// <param name="projectionName">The projection type name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="strategy">The rebuild strategy to use.</param>
    /// <param name="timeout">Timeout before the rebuild is considered stuck.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A rebuild token for tracking the rebuild.</returns>
    Task<RebuildToken> StartRebuildAsync(
        string projectionName,
        string objectId,
        RebuildStrategy strategy,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the catch-up phase after initial rebuild (blocking strategy only).
    /// </summary>
    /// <param name="token">The rebuild token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartCatchUpAsync(
        RebuildToken token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the projection as ready (blue-green strategy).
    /// </summary>
    /// <param name="token">The rebuild token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkReadyAsync(
        RebuildToken token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a rebuild, setting status to Active.
    /// </summary>
    /// <param name="token">The rebuild token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CompleteRebuildAsync(
        RebuildToken token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a rebuild, reverting status to Active.
    /// </summary>
    /// <param name="token">The rebuild token.</param>
    /// <param name="error">Optional error message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelRebuildAsync(
        RebuildToken token,
        string? error = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a projection.
    /// </summary>
    /// <param name="projectionName">The projection type name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The projection status info, or null if not found.</returns>
    Task<ProjectionStatusInfo?> GetStatusAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all projections with the specified status.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of projection status info.</returns>
    Task<IEnumerable<ProjectionStatusInfo>> GetByStatusAsync(
        ProjectionStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers stuck rebuilds that have timed out.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rebuilds recovered.</returns>
    Task<int> RecoverStuckRebuildsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables a projection.
    /// </summary>
    /// <param name="projectionName">The projection type name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables a disabled projection (sets to Active).
    /// </summary>
    /// <param name="projectionName">The projection type name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Token representing an ongoing rebuild operation.
/// </summary>
/// <param name="ProjectionName">The projection type name.</param>
/// <param name="ObjectId">The object identifier.</param>
/// <param name="Token">Unique token for this rebuild.</param>
/// <param name="Strategy">The rebuild strategy being used.</param>
/// <param name="StartedAt">When the rebuild started.</param>
/// <param name="ExpiresAt">When the rebuild times out.</param>
public record RebuildToken(
    string ProjectionName,
    string ObjectId,
    string Token,
    RebuildStrategy Strategy,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// Gets whether the rebuild token has expired.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    /// <summary>
    /// Creates a new rebuild token.
    /// </summary>
    public static RebuildToken Create(
        string projectionName,
        string objectId,
        RebuildStrategy strategy,
        TimeSpan timeout)
    {
        var now = DateTimeOffset.UtcNow;
        return new RebuildToken(
            projectionName,
            objectId,
            Guid.NewGuid().ToString("N"),
            strategy,
            now,
            now.Add(timeout));
    }
}

/// <summary>
/// Information about a projection's status.
/// </summary>
/// <param name="ProjectionName">The projection type name.</param>
/// <param name="ObjectId">The object identifier.</param>
/// <param name="Status">The current status.</param>
/// <param name="StatusChangedAt">When the status was last changed.</param>
/// <param name="SchemaVersion">The stored schema version.</param>
/// <param name="RebuildInfo">Information about any ongoing rebuild.</param>
public record ProjectionStatusInfo(
    string ProjectionName,
    string ObjectId,
    ProjectionStatus Status,
    DateTimeOffset? StatusChangedAt,
    int SchemaVersion,
    RebuildInfo? RebuildInfo = null);
