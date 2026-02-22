namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Contains metadata about an ongoing or completed projection rebuild.
/// </summary>
/// <param name="Strategy">The rebuild strategy being used.</param>
/// <param name="StartedAt">When the rebuild was initiated.</param>
/// <param name="SourceVersion">The schema version being rebuilt from (for blue-green).</param>
/// <param name="SourceCheckpointFingerprint">Checkpoint fingerprint of source at rebuild start.</param>
/// <param name="LastUpdatedAt">When the rebuild progress was last updated.</param>
/// <param name="CompletedAt">When the rebuild completed (null if in progress).</param>
/// <param name="Error">Error message if the rebuild failed.</param>
public record RebuildInfo(
    RebuildStrategy Strategy,
    DateTimeOffset StartedAt,
    int SourceVersion,
    string? SourceCheckpointFingerprint,
    DateTimeOffset LastUpdatedAt,
    DateTimeOffset? CompletedAt,
    string? Error)
{
    /// <summary>
    /// Creates a new RebuildInfo for starting a rebuild.
    /// </summary>
    /// <param name="strategy">The rebuild strategy to use.</param>
    /// <param name="sourceVersion">The schema version being rebuilt from.</param>
    /// <param name="sourceCheckpointFingerprint">Current checkpoint fingerprint of source.</param>
    /// <returns>A new RebuildInfo instance.</returns>
    public static RebuildInfo Start(
        RebuildStrategy strategy,
        int sourceVersion = 0,
        string? sourceCheckpointFingerprint = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new RebuildInfo(
            strategy,
            now,
            sourceVersion,
            sourceCheckpointFingerprint,
            now,
            null,
            null);
    }

    /// <summary>
    /// Creates an updated RebuildInfo with the current timestamp.
    /// </summary>
    public RebuildInfo WithProgress() =>
        this with { LastUpdatedAt = DateTimeOffset.UtcNow };

    /// <summary>
    /// Creates an updated RebuildInfo marking completion.
    /// </summary>
    public RebuildInfo WithCompletion() =>
        this with { CompletedAt = DateTimeOffset.UtcNow, LastUpdatedAt = DateTimeOffset.UtcNow };

    /// <summary>
    /// Creates an updated RebuildInfo marking failure.
    /// </summary>
    /// <param name="error">The error message.</param>
    public RebuildInfo WithError(string error) =>
        this with { Error = error, LastUpdatedAt = DateTimeOffset.UtcNow };

    /// <summary>
    /// Gets whether the rebuild is still in progress.
    /// </summary>
    public bool IsInProgress => CompletedAt is null && Error is null;

    /// <summary>
    /// Gets whether the rebuild completed successfully.
    /// </summary>
    public bool IsSuccessful => CompletedAt is not null && Error is null;

    /// <summary>
    /// Gets whether the rebuild failed.
    /// </summary>
    public bool IsFailed => Error is not null;

    /// <summary>
    /// Gets the duration of the rebuild (or time elapsed if still in progress).
    /// </summary>
    public TimeSpan Duration => (CompletedAt ?? DateTimeOffset.UtcNow) - StartedAt;
}

/// <summary>
/// Strategy for rebuilding projections.
/// </summary>
public enum RebuildStrategy
{
    /// <summary>
    /// Block inline updates during rebuild, use convergent catch-up.
    /// Simpler but has brief blocking window.
    /// </summary>
    BlockingWithCatchUp = 0,

    /// <summary>
    /// Build new version in parallel, switch when caught up.
    /// No blocking, safe rollback, recommended for production.
    /// </summary>
    BlueGreen = 1
}
