using System.Diagnostics;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Default implementation of <see cref="ISnapshotCleanupService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Cleanup is performed based on the policy's retention settings:
/// - <see cref="SnapshotPolicy.KeepSnapshots"/>: Maximum number of snapshots to retain
/// - <see cref="SnapshotPolicy.MaxAge"/>: Maximum age for snapshots
/// </para>
/// <para>
/// The most recent snapshots are always retained up to the KeepSnapshots limit.
/// Snapshots older than MaxAge are deleted regardless of the KeepSnapshots setting,
/// except the most recent snapshot is always kept.
/// </para>
/// </remarks>
public class SnapshotCleanupService : ISnapshotCleanupService
{
    private readonly ISnapShotStore _snapshotStore;
    private readonly ISnapshotPolicyProvider _policyProvider;
    private readonly ILogger<SnapshotCleanupService>? _logger;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.Snapshots");

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotCleanupService"/> class.
    /// </summary>
    /// <param name="snapshotStore">The snapshot store.</param>
    /// <param name="policyProvider">The policy provider.</param>
    /// <param name="logger">Optional logger.</param>
    public SnapshotCleanupService(
        ISnapShotStore snapshotStore,
        ISnapshotPolicyProvider policyProvider,
        ILogger<SnapshotCleanupService>? logger = null)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SnapshotCleanupResult> CleanupAsync(
        IObjectDocument document,
        Type aggregateType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(aggregateType);

        var policy = _policyProvider.GetPolicy(aggregateType);
        if (policy is null || !policy.Enabled)
        {
            return SnapshotCleanupResult.NoPolicyConfigured();
        }

        return await CleanupAsync(document, policy, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SnapshotCleanupResult> CleanupAsync(
        IObjectDocument document,
        SnapshotPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(policy);

        using var activity = ActivitySource.StartActivity("SnapshotCleanupService.Cleanup");
        activity?.SetTag("StreamId", document.Active.StreamIdentifier);

        // No retention policy - nothing to clean
        if (policy.KeepSnapshots <= 0 && policy.MaxAge is null)
        {
            return SnapshotCleanupResult.NoPolicyConfigured();
        }

        // List all snapshots
        var snapshots = await _snapshotStore.ListSnapshotsAsync(document, cancellationToken);
        if (snapshots.Count == 0)
        {
            return SnapshotCleanupResult.NoCleanupNeeded(0);
        }

        // Determine which snapshots to delete
        var toDelete = new List<SnapshotMetadata>();
        var toRetain = new List<SnapshotMetadata>();

        // Snapshots are ordered by version descending (most recent first)
        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            var shouldDelete = false;

            // Always keep at least one snapshot (the most recent)
            if (i == 0)
            {
                toRetain.Add(snapshot);
                continue;
            }

            // Check count limit
            if (policy.KeepSnapshots > 0 && i >= policy.KeepSnapshots)
            {
                shouldDelete = true;
            }

            // Check age limit
            if (policy.MaxAge is not null && snapshot.IsOlderThan(policy.MaxAge.Value))
            {
                shouldDelete = true;
            }

            if (shouldDelete)
            {
                toDelete.Add(snapshot);
            }
            else
            {
                toRetain.Add(snapshot);
            }
        }

        // Nothing to delete
        if (toDelete.Count == 0)
        {
            return SnapshotCleanupResult.NoCleanupNeeded(toRetain.Count);
        }

        // Delete the snapshots
        var versions = toDelete.Select(s => s.Version).ToList();
        var deleted = await _snapshotStore.DeleteManyAsync(document, versions, cancellationToken);

        _logger?.LogInformation(
            "Cleaned up {DeletedCount} snapshots for {StreamId}. Retained: {RetainedCount}",
            deleted,
            document.Active.StreamIdentifier,
            toRetain.Count);

        activity?.SetTag("SnapshotsDeleted", deleted);
        activity?.SetTag("SnapshotsRetained", toRetain.Count);

        var deleteReason = policy.MaxAge is not null
            ? $"Age limit ({policy.MaxAge}) and/or count limit ({policy.KeepSnapshots})"
            : $"Count limit ({policy.KeepSnapshots})";

        return SnapshotCleanupResult.Cleaned(deleted, toRetain.Count, versions, deleteReason);
    }
}
