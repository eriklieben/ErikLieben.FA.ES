namespace ErikLieben.FA.ES.EventStreamManagement.Core;

/// <summary>
/// Represents the current status of a migration operation.
/// </summary>
public enum MigrationStatus
{
    /// <summary>
    /// Migration is pending and has not started yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Migration is currently in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Migration has been paused and can be resumed.
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Migration is performing verification checks.
    /// </summary>
    Verifying = 3,

    /// <summary>
    /// Migration is performing the atomic cutover.
    /// </summary>
    CuttingOver = 4,

    /// <summary>
    /// Migration completed successfully.
    /// </summary>
    Completed = 5,

    /// <summary>
    /// Migration failed with errors.
    /// </summary>
    Failed = 6,

    /// <summary>
    /// Migration was cancelled by user or system.
    /// </summary>
    Cancelled = 7,

    /// <summary>
    /// Migration is being rolled back due to errors.
    /// </summary>
    RollingBack = 8,

    /// <summary>
    /// Migration was rolled back successfully.
    /// </summary>
    RolledBack = 9
}
