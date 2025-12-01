namespace ErikLieben.FA.ES.EventStreamManagement.Progress;

using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;

/// <summary>
/// Provides progress information for an ongoing migration operation.
/// </summary>
public interface IMigrationProgress
{
    /// <summary>
    /// Gets the unique identifier for this migration.
    /// </summary>
    Guid MigrationId { get; }

    /// <summary>
    /// Gets the current status of the migration.
    /// </summary>
    MigrationStatus Status { get; }

    /// <summary>
    /// Gets the current migration phase.
    /// </summary>
    MigrationPhase CurrentPhase { get; }

    /// <summary>
    /// Gets the completion percentage (0-100).
    /// </summary>
    double PercentageComplete { get; }

    /// <summary>
    /// Gets the number of events processed so far.
    /// </summary>
    long EventsProcessed { get; }

    /// <summary>
    /// Gets the total number of events to process.
    /// </summary>
    long TotalEvents { get; }

    /// <summary>
    /// Gets the current event processing rate (events per second).
    /// </summary>
    double EventsPerSecond { get; }

    /// <summary>
    /// Gets the elapsed time since migration started.
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets the estimated time remaining, if calculable.
    /// </summary>
    TimeSpan? EstimatedRemaining { get; }

    /// <summary>
    /// Gets a value indicating whether the migration is paused.
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Gets a value indicating whether the migration can be paused.
    /// </summary>
    bool CanPause { get; }

    /// <summary>
    /// Gets a value indicating whether the migration can be rolled back.
    /// </summary>
    bool CanRollback { get; }

    /// <summary>
    /// Gets custom metrics specific to this migration.
    /// </summary>
    IReadOnlyDictionary<string, object> CustomMetrics { get; }

    /// <summary>
    /// Gets error details if the migration failed.
    /// </summary>
    string? ErrorMessage { get; }
}
