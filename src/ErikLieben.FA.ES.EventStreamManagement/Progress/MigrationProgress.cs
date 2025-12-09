namespace ErikLieben.FA.ES.EventStreamManagement.Progress;

using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;

/// <summary>
/// Implementation of migration progress tracking.
/// </summary>
public class MigrationProgress : IMigrationProgress
{
    /// <inheritdoc/>
    public Guid MigrationId { get; init; }

    /// <inheritdoc/>
    public MigrationStatus Status { get; set; }

    /// <inheritdoc/>
    public MigrationPhase CurrentPhase { get; set; }

    /// <inheritdoc/>
    public double PercentageComplete { get; set; }

    /// <inheritdoc/>
    public long EventsProcessed { get; set; }

    /// <inheritdoc/>
    public long TotalEvents { get; set; }

    /// <inheritdoc/>
    public double EventsPerSecond { get; set; }

    /// <inheritdoc/>
    public TimeSpan Elapsed { get; set; }

    /// <inheritdoc/>
    public TimeSpan? EstimatedRemaining { get; set; }

    /// <inheritdoc/>
    public bool IsPaused { get; set; }

    /// <inheritdoc/>
    public bool CanPause { get; set; }

    /// <inheritdoc/>
    public bool CanRollback { get; set; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object> CustomMetrics { get; set; } =
        new Dictionary<string, object>();

    /// <inheritdoc/>
    public string? ErrorMessage { get; set; }
}
