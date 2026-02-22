namespace TaskFlow.Domain.ValueObjects.Project;

/// <summary>
/// Represents the final outcome state of a completed project
/// </summary>
public enum ProjectOutcome
{
    /// <summary>
    /// Project is still active (not completed)
    /// </summary>
    None = 0,

    /// <summary>
    /// Project completed successfully with all objectives met
    /// </summary>
    Successful = 1,

    /// <summary>
    /// Project was cancelled before completion
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// Project failed to meet its objectives
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Project was delivered to production/client
    /// </summary>
    Delivered = 4,

    /// <summary>
    /// Project was suspended/put on hold
    /// </summary>
    Suspended = 5,

    /// <summary>
    /// Project was merged into another project
    /// </summary>
    Merged = 6
}
