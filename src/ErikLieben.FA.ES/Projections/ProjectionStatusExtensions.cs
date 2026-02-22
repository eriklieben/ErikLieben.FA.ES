namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Extension methods for <see cref="ProjectionStatus"/>.
/// </summary>
public static class ProjectionStatusExtensions
{
    /// <summary>
    /// Returns true if inline updates should be processed for this status.
    /// </summary>
    /// <param name="status">The projection status.</param>
    /// <returns>True if inline updates should be processed.</returns>
    public static bool ShouldProcessInlineUpdates(this ProjectionStatus status)
    {
        return status == ProjectionStatus.Active;
    }

    /// <summary>
    /// Returns true if the projection is in a transitional state
    /// (rebuild in progress, catching up, or ready to switch).
    /// </summary>
    /// <param name="status">The projection status.</param>
    /// <returns>True if transitioning.</returns>
    public static bool IsTransitioning(this ProjectionStatus status)
    {
        return status is ProjectionStatus.Rebuilding
                      or ProjectionStatus.CatchingUp
                      or ProjectionStatus.Ready;
    }

    /// <summary>
    /// Returns true if the projection can be queried (has valid data).
    /// Active, Ready, and Archived projections all have queryable data.
    /// </summary>
    /// <param name="status">The projection status.</param>
    /// <returns>True if queryable.</returns>
    public static bool IsQueryable(this ProjectionStatus status)
    {
        return status is ProjectionStatus.Active
                      or ProjectionStatus.Ready
                      or ProjectionStatus.Archived;
    }

    /// <summary>
    /// Returns true if the projection needs attention (failed or disabled).
    /// </summary>
    /// <param name="status">The projection status.</param>
    /// <returns>True if attention needed.</returns>
    public static bool NeedsAttention(this ProjectionStatus status)
    {
        return status is ProjectionStatus.Failed
                      or ProjectionStatus.Disabled;
    }

    /// <summary>
    /// Returns true if the projection is currently processing a rebuild.
    /// </summary>
    /// <param name="status">The projection status.</param>
    /// <returns>True if rebuilding.</returns>
    public static bool IsRebuilding(this ProjectionStatus status)
    {
        return status is ProjectionStatus.Rebuilding
                      or ProjectionStatus.CatchingUp;
    }

    /// <summary>
    /// Returns true if the projection is in a terminal state
    /// (not expected to change without manual intervention).
    /// </summary>
    /// <param name="status">The projection status.</param>
    /// <returns>True if in terminal state.</returns>
    public static bool IsTerminal(this ProjectionStatus status)
    {
        return status is ProjectionStatus.Active
                      or ProjectionStatus.Disabled
                      or ProjectionStatus.Archived
                      or ProjectionStatus.Failed;
    }

    /// <summary>
    /// Gets a human-readable description of the status.
    /// </summary>
    /// <param name="status">The projection status.</param>
    /// <returns>A description string.</returns>
    public static string GetDescription(this ProjectionStatus status)
    {
        return status switch
        {
            ProjectionStatus.Active => "Active and processing events normally",
            ProjectionStatus.Rebuilding => "Rebuild in progress",
            ProjectionStatus.Disabled => "Disabled, not processing events",
            ProjectionStatus.CatchingUp => "Catching up after rebuild",
            ProjectionStatus.Ready => "Ready to become active",
            ProjectionStatus.Archived => "Archived (previous version)",
            ProjectionStatus.Failed => "Failed, needs investigation",
            _ => "Unknown status"
        };
    }
}
