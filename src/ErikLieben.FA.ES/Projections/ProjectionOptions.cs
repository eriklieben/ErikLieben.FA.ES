namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Configuration options for projection behavior.
/// </summary>
public class ProjectionOptions
{
    /// <summary>
    /// Gets or sets the behavior when a schema version mismatch is detected.
    /// Default is <see cref="SchemaMismatchBehavior.Warn"/>.
    /// </summary>
    public SchemaMismatchBehavior SchemaMismatchBehavior { get; set; } = SchemaMismatchBehavior.Warn;

    /// <summary>
    /// Gets or sets the default rebuild strategy when auto-rebuild is enabled.
    /// Default is <see cref="RebuildStrategy.BlockingWithCatchUp"/>.
    /// </summary>
    public RebuildStrategy DefaultRebuildStrategy { get; set; } = RebuildStrategy.BlockingWithCatchUp;

    /// <summary>
    /// Gets or sets the timeout for rebuild operations before they are considered stuck.
    /// Default is 1 hour.
    /// </summary>
    public TimeSpan RebuildTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets whether to automatically recover stuck rebuilds.
    /// Default is true.
    /// </summary>
    public bool AutoRecoverStuckRebuilds { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval at which to check for stuck rebuilds.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan StuckRebuildCheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long to retain archived projection versions.
    /// Default is 7 days.
    /// </summary>
    public TimeSpan ArchivedVersionRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the maximum number of catch-up iterations before giving up.
    /// Default is 10.
    /// </summary>
    public int MaxCatchUpIterations { get; set; } = 10;

    /// <summary>
    /// Gets or sets the delay between catch-up iterations.
    /// Default is 100ms.
    /// </summary>
    public TimeSpan CatchUpIterationDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
