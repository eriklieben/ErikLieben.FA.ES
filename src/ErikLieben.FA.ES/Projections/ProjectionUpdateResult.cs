namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Represents the result of an <see cref="Projection.UpdateToVersion"/> operation.
/// </summary>
/// <remarks>
/// When a projection's status is <see cref="ProjectionStatus.Rebuilding"/> or <see cref="ProjectionStatus.Disabled"/>,
/// updates are skipped and this result contains information about the skipped update.
/// Consuming applications can use this information to queue skipped events for retry after the rebuild completes.
/// </remarks>
public record ProjectionUpdateResult
{
    /// <summary>
    /// Gets whether the update was skipped due to the projection's status.
    /// </summary>
    public bool Skipped { get; init; }

    /// <summary>
    /// Gets the projection status that caused the update to be skipped.
    /// Only relevant when <see cref="Skipped"/> is true.
    /// </summary>
    public ProjectionStatus Status { get; init; }

    /// <summary>
    /// Gets the version token that was skipped.
    /// Consuming applications can use this to queue the event for retry.
    /// Only populated when <see cref="Skipped"/> is true.
    /// </summary>
    public VersionToken? SkippedToken { get; init; }

    private static readonly ProjectionUpdateResult SuccessInstance = new() { Skipped = false, Status = ProjectionStatus.Active };

    /// <summary>
    /// Gets a successful result indicating the update was processed normally.
    /// </summary>
    public static ProjectionUpdateResult Success => SuccessInstance;

    /// <summary>
    /// Creates a result indicating the update was skipped due to the projection's status.
    /// </summary>
    /// <param name="status">The status that caused the skip.</param>
    /// <param name="token">The version token that was skipped.</param>
    /// <returns>A new <see cref="ProjectionUpdateResult"/> indicating a skipped update.</returns>
    public static ProjectionUpdateResult SkippedDueToStatus(ProjectionStatus status, VersionToken token)
        => new() { Skipped = true, Status = status, SkippedToken = token };
}
