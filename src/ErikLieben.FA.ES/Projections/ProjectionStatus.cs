namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Represents the operational status of a projection.
/// </summary>
/// <remarks>
/// Use this status to coordinate projection rebuilds with inline updates.
/// When a projection is being rebuilt, inline updates should be skipped to avoid
/// conflicts and ensure the rebuild processes all events in order.
/// </remarks>
public enum ProjectionStatus
{
    /// <summary>
    /// Projection is active and processing events normally.
    /// Inline updates are processed immediately.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Projection is being rebuilt from historical events.
    /// Behavior depends on strategy:
    /// - Blocking: inline updates skip
    /// - Blue-Green: this is a new version being built, doesn't affect active
    /// </summary>
    Rebuilding = 1,

    /// <summary>
    /// Projection is disabled and not processing any updates.
    /// All updates are skipped.
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// Post-rebuild catch-up phase (blocking strategy only).
    /// Processing events that arrived during rebuild.
    /// Inline updates are still skipped.
    /// </summary>
    CatchingUp = 3,

    /// <summary>
    /// Blue-green: new version has caught up, ready to become active.
    /// Awaiting manual or automatic switch.
    /// </summary>
    Ready = 4,

    /// <summary>
    /// Previous version kept for rollback after blue-green switch.
    /// No longer processing, but data retained for potential rollback.
    /// </summary>
    Archived = 5,

    /// <summary>
    /// Rebuild or catch-up failed. Needs investigation.
    /// Inline updates skip to prevent further issues.
    /// </summary>
    Failed = 6
}
