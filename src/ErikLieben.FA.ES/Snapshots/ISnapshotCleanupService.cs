using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Service for cleaning up old snapshots based on retention policies.
/// </summary>
public interface ISnapshotCleanupService
{
    /// <summary>
    /// Cleans up old snapshots for a stream based on the aggregate's policy.
    /// </summary>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="aggregateType">The aggregate type for policy lookup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cleanup result.</returns>
    Task<SnapshotCleanupResult> CleanupAsync(
        IObjectDocument document,
        Type aggregateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old snapshots using the specified policy.
    /// </summary>
    /// <param name="document">The object document identifying the stream.</param>
    /// <param name="policy">The snapshot policy to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cleanup result.</returns>
    Task<SnapshotCleanupResult> CleanupAsync(
        IObjectDocument document,
        SnapshotPolicy policy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a snapshot cleanup operation.
/// </summary>
/// <param name="SnapshotsDeleted">Number of snapshots deleted.</param>
/// <param name="SnapshotsRetained">Number of snapshots retained.</param>
/// <param name="DeletedVersions">Versions of deleted snapshots.</param>
/// <param name="Reason">Description of cleanup action taken.</param>
public record SnapshotCleanupResult(
    int SnapshotsDeleted,
    int SnapshotsRetained,
    IReadOnlyList<int> DeletedVersions,
    string Reason)
{
    /// <summary>
    /// Creates a result indicating no cleanup was needed.
    /// </summary>
    public static SnapshotCleanupResult NoCleanupNeeded(int retained)
        => new(0, retained, [], "Retention limits not exceeded");

    /// <summary>
    /// Creates a result indicating snapshots were cleaned up.
    /// </summary>
    public static SnapshotCleanupResult Cleaned(int deleted, int retained, IReadOnlyList<int> deletedVersions, string reason)
        => new(deleted, retained, deletedVersions, reason);

    /// <summary>
    /// Creates a result indicating no policy was configured.
    /// </summary>
    public static SnapshotCleanupResult NoPolicyConfigured()
        => new(0, 0, [], "No cleanup policy configured");
}
