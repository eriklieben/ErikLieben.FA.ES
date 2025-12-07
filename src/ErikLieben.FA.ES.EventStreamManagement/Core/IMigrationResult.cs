namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Verification;

/// <summary>
/// Represents the result of a migration operation.
/// </summary>
public interface IMigrationResult
{
    /// <summary>
    /// Gets the unique identifier for the migration.
    /// </summary>
    Guid MigrationId { get; }

    /// <summary>
    /// Gets a value indicating whether the migration was successful.
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Gets the final status of the migration.
    /// </summary>
    MigrationStatus Status { get; }

    /// <summary>
    /// Gets error information if the migration failed.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the exception that caused the failure, if any.
    /// </summary>
    Exception? Exception { get; }

    /// <summary>
    /// Gets the final progress snapshot.
    /// </summary>
    IMigrationProgress Progress { get; }

    /// <summary>
    /// Gets the verification result if verification was performed.
    /// </summary>
    IVerificationResult? VerificationResult { get; }

    /// <summary>
    /// Gets the migration plan if this was a dry run.
    /// </summary>
    IMigrationPlan? Plan { get; }

    /// <summary>
    /// Gets the total duration of the migration.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Gets statistics about the migration.
    /// </summary>
    MigrationStatistics Statistics { get; }
}

/// <summary>
/// Contains statistical information about a migration.
/// </summary>
public record MigrationStatistics
{
    /// <summary>
    /// Gets or sets the total number of events migrated.
    /// </summary>
    public long TotalEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of events transformed.
    /// </summary>
    public long EventsTransformed { get; set; }

    /// <summary>
    /// Gets or sets the number of events that failed transformation.
    /// </summary>
    public long TransformationFailures { get; set; }

    /// <summary>
    /// Gets or sets the average events per second throughput.
    /// </summary>
    public double AverageEventsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the total bytes processed.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when migration started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when migration completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
