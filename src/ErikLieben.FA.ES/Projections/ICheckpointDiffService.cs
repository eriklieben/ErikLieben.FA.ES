namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Compares and synchronizes projection checkpoints.
/// Used during blue-green deployments to identify and apply missing events.
/// </summary>
public interface ICheckpointDiffService
{
    /// <summary>
    /// Compares checkpoints between source (active) and target (rebuilding).
    /// Fast path: returns immediately if fingerprints match.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="sourceVersion">The source schema version.</param>
    /// <param name="targetVersion">The target schema version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The comparison result.</returns>
    Task<CheckpointComparisonResult> CompareAsync<T>(
        string objectId,
        int sourceVersion,
        int targetVersion,
        CancellationToken cancellationToken = default)
        where T : Projection;

    /// <summary>
    /// Applies missing events from source to target checkpoint.
    /// Returns updated comparison result.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="sourceVersion">The source schema version.</param>
    /// <param name="targetVersion">The target schema version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The comparison result after sync.</returns>
    Task<CheckpointComparisonResult> SyncAsync<T>(
        string objectId,
        int sourceVersion,
        int targetVersion,
        CancellationToken cancellationToken = default)
        where T : Projection;

    /// <summary>
    /// Performs convergent catch-up until checkpoints match or max iterations reached.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="sourceVersion">The source schema version.</param>
    /// <param name="targetVersion">The target schema version.</param>
    /// <param name="options">Catch-up options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The catch-up result.</returns>
    Task<ConvergentCatchUpResult> ConvergentCatchUpAsync<T>(
        string objectId,
        int sourceVersion,
        int targetVersion,
        ConvergentCatchUpOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : Projection;
}

/// <summary>
/// Result of comparing two projection checkpoints.
/// </summary>
/// <param name="IsSynced">Whether the checkpoints are synchronized.</param>
/// <param name="SourceFingerprint">The source checkpoint fingerprint.</param>
/// <param name="TargetFingerprint">The target checkpoint fingerprint.</param>
/// <param name="Diff">The detailed diff, if not synced.</param>
public record CheckpointComparisonResult(
    bool IsSynced,
    string? SourceFingerprint,
    string? TargetFingerprint,
    CheckpointDiff? Diff)
{
    /// <summary>
    /// Creates a result indicating checkpoints are synced.
    /// </summary>
    public static CheckpointComparisonResult Synced(string fingerprint) =>
        new(true, fingerprint, fingerprint, null);

    /// <summary>
    /// Creates a result indicating checkpoints differ.
    /// </summary>
    public static CheckpointComparisonResult Different(
        string sourceFingerprint,
        string targetFingerprint,
        CheckpointDiff diff) =>
        new(false, sourceFingerprint, targetFingerprint, diff);
}

/// <summary>
/// Detailed diff between two checkpoints.
/// </summary>
/// <param name="StreamDiffs">Differences per stream.</param>
/// <param name="TotalMissingEvents">Total estimated missing events.</param>
/// <param name="MissingStreams">Streams completely missing from target.</param>
public record CheckpointDiff(
    IReadOnlyList<StreamDiff> StreamDiffs,
    int TotalMissingEvents,
    IReadOnlyList<string> MissingStreams);

/// <summary>
/// Diff for a single stream.
/// </summary>
/// <param name="StreamId">The stream identifier.</param>
/// <param name="SourceToken">The source version token.</param>
/// <param name="TargetToken">The target version token (null if missing).</param>
/// <param name="EstimatedMissingEvents">Estimated number of missing events.</param>
public record StreamDiff(
    string StreamId,
    string SourceToken,
    string? TargetToken,
    int EstimatedMissingEvents);

/// <summary>
/// Result of a convergent catch-up operation.
/// </summary>
/// <param name="IsSynced">Whether synchronization was achieved.</param>
/// <param name="IterationsPerformed">Number of iterations performed.</param>
/// <param name="TotalEventsApplied">Total events applied during catch-up.</param>
/// <param name="Duration">Total duration of the operation.</param>
/// <param name="FailureReason">Reason for failure, if any.</param>
public record ConvergentCatchUpResult(
    bool IsSynced,
    int IterationsPerformed,
    int TotalEventsApplied,
    TimeSpan Duration,
    string? FailureReason)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ConvergentCatchUpResult Success(int iterations, int eventsApplied, TimeSpan duration) =>
        new(true, iterations, eventsApplied, duration, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ConvergentCatchUpResult Failed(int iterations, int eventsApplied, TimeSpan duration, string reason) =>
        new(false, iterations, eventsApplied, duration, reason);
}

/// <summary>
/// Options for convergent catch-up.
/// </summary>
public class ConvergentCatchUpOptions
{
    /// <summary>
    /// Maximum catch-up iterations before giving up. Default: 10
    /// </summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// Delay between iterations to allow events to settle. Default: 100ms
    /// </summary>
    public TimeSpan IterationDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Abort if a single iteration processes more than this many events.
    /// Indicates high traffic, may never converge. Default: 1000
    /// </summary>
    public int MaxEventsPerIteration { get; set; } = 1000;
}
