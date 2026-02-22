namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Specifies how to handle schema version mismatches when loading projections.
/// </summary>
public enum SchemaMismatchBehavior
{
    /// <summary>
    /// Throw an exception when schema version mismatch is detected.
    /// Use this in production to fail fast and ensure manual intervention.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Log a warning and continue with the existing projection data.
    /// The projection will be loaded but may have stale schema.
    /// </summary>
    Warn = 1,

    /// <summary>
    /// Automatically trigger a rebuild when schema mismatch is detected.
    /// Use with caution - can cause unexpected rebuilds in production.
    /// </summary>
    AutoRebuild = 2
}
