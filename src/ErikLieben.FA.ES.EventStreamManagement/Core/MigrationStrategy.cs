namespace ErikLieben.FA.ES.EventStreamManagement.Core;

/// <summary>
/// Defines the strategy used for migrating event streams.
/// </summary>
public enum MigrationStrategy
{
    /// <summary>
    /// Copy events from old stream to new stream with optional transformations.
    /// This is the safest approach for zero-downtime migrations.
    /// </summary>
    CopyAndTransform = 0,

    /// <summary>
    /// Transform events on-demand during read operations and cache the result.
    /// First read is slower, but subsequent reads use the cached transformed version.
    /// </summary>
    LazyTransform = 1,

    /// <summary>
    /// Directly mutate events in the existing stream (risky).
    /// This can require brief downtime and should be used with caution.
    /// </summary>
    InPlaceTransform = 2
}
