namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Global configuration options for automatic snapshots.
/// </summary>
public class SnapshotOptions
{
    /// <summary>
    /// Gets or sets the default policy for aggregates without an explicit
    /// <see cref="Attributes.SnapshotPolicyAttribute"/>.
    /// Set to null to disable automatic snapshots by default.
    /// </summary>
    public SnapshotPolicy? DefaultPolicy { get; set; }

    /// <summary>
    /// Gets or sets the timeout for snapshot creation operations.
    /// If exceeded, the operation logs a warning but does not fail the event append.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets whether snapshot creation failures should be logged as warnings.
    /// When false, failures are logged as debug messages only.
    /// Default: true.
    /// </summary>
    public bool LogFailuresAsWarnings { get; set; } = true;

    /// <summary>
    /// Gets or sets per-aggregate type policy overrides.
    /// Keys should be the full type name of the aggregate.
    /// </summary>
    public Dictionary<string, SnapshotPolicy> PolicyOverrides { get; set; } = [];

    /// <summary>
    /// Gets default options with no automatic snapshots enabled.
    /// </summary>
    public static SnapshotOptions Default { get; } = new();
}
