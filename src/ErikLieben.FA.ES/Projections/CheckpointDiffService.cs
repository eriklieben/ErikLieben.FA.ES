using System.Diagnostics;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.VersionTokenParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Default implementation of <see cref="ICheckpointDiffService"/> that compares and synchronizes
/// projection checkpoints between schema versions for blue-green deployments.
/// </summary>
public class CheckpointDiffService : ICheckpointDiffService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CheckpointDiffService>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointDiffService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving projection factories.</param>
    /// <param name="logger">Optional logger.</param>
    public CheckpointDiffService(
        IServiceProvider serviceProvider,
        ILogger<CheckpointDiffService>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CheckpointComparisonResult> CompareAsync<T>(
        string objectId,
        int sourceVersion,
        int targetVersion,
        CancellationToken cancellationToken = default)
        where T : Projection
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("CheckpointDiff.Compare");

        var factory = _serviceProvider.GetRequiredService<IProjectionFactory<T>>();
        var documentFactory = _serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var eventStreamFactory = _serviceProvider.GetRequiredService<IEventStreamFactory>();

        var sourceBlobName = GetVersionedBlobName<T>(sourceVersion);
        var targetBlobName = GetVersionedBlobName<T>(targetVersion);

        var source = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory, sourceBlobName, cancellationToken);
        var target = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory, targetBlobName, cancellationToken);

        return CompareCheckpoints(source, target);
    }

    /// <inheritdoc />
    public async Task<CheckpointComparisonResult> SyncAsync<T>(
        string objectId,
        int sourceVersion,
        int targetVersion,
        CancellationToken cancellationToken = default)
        where T : Projection
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("CheckpointDiff.Sync");

        var factory = _serviceProvider.GetRequiredService<IProjectionFactory<T>>();
        var documentFactory = _serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var eventStreamFactory = _serviceProvider.GetRequiredService<IEventStreamFactory>();

        var sourceBlobName = GetVersionedBlobName<T>(sourceVersion);
        var targetBlobName = GetVersionedBlobName<T>(targetVersion);

        var source = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory, sourceBlobName, cancellationToken);
        var target = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory, targetBlobName, cancellationToken);

        var comparison = CompareCheckpoints(source, target);

        if (comparison.IsSynced)
        {
            if (_logger is not null)
            {
                _logger.LogDebug(
                    "Checkpoints already synced for {ProjectionType} between v{Source} and v{Target}",
                    typeof(T).Name, sourceVersion, targetVersion);
            }

            return comparison;
        }

        // Apply missing events to target for streams with version differences
        foreach (var streamDiff in comparison.Diff!.StreamDiffs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var objId = new ObjectIdentifier(streamDiff.StreamId);
            if (source.Checkpoint.TryGetValue(objId, out var sourceVersionId))
            {
                var token = new VersionToken(objId, sourceVersionId).ToLatestVersion();
                await target.UpdateToVersion(token);
            }
        }

        // Handle streams completely missing from target
        foreach (var missingStream in comparison.Diff.MissingStreams)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var objId = new ObjectIdentifier(missingStream);
            if (source.Checkpoint.TryGetValue(objId, out var sourceVersionId))
            {
                var token = new VersionToken(objId, sourceVersionId).ToLatestVersion();
                await target.UpdateToVersion(token);
            }
        }

        await factory.SaveAsync(target, targetBlobName, cancellationToken);

        if (_logger is not null)
        {
            _logger.LogInformation(
                "Synced {ProjectionType} from v{Source} to v{Target}, applied diffs for {StreamCount} streams",
                typeof(T).Name, sourceVersion, targetVersion,
                comparison.Diff.StreamDiffs.Count + comparison.Diff.MissingStreams.Count);
        }

        // Return updated comparison
        return CompareCheckpoints(source, target);
    }

    /// <inheritdoc />
    public async Task<ConvergentCatchUpResult> ConvergentCatchUpAsync<T>(
        string objectId,
        int sourceVersion,
        int targetVersion,
        ConvergentCatchUpOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : Projection
    {
        options ??= new ConvergentCatchUpOptions();
        var sw = Stopwatch.StartNew();
        var totalEventsApplied = 0;

        using var activity = FaesInstrumentation.Projections.StartActivity("CheckpointDiff.ConvergentCatchUp");

        for (var iteration = 1; iteration <= options.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var comparison = await CompareAsync<T>(objectId, sourceVersion, targetVersion, cancellationToken);

            if (comparison.IsSynced)
            {
                sw.Stop();
                if (_logger is not null)
                {
                    _logger.LogInformation(
                        "Convergent catch-up for {ProjectionType} completed in {Iterations} iterations, {Events} events, {Duration}ms",
                        typeof(T).Name, iteration, totalEventsApplied, sw.ElapsedMilliseconds);
                }

                return ConvergentCatchUpResult.Success(iteration, totalEventsApplied, sw.Elapsed);
            }

            var eventsInIteration = comparison.Diff?.TotalMissingEvents ?? 0;

            if (eventsInIteration > options.MaxEventsPerIteration)
            {
                sw.Stop();
                var reason = $"Too many events in single iteration ({eventsInIteration} > {options.MaxEventsPerIteration}), may never converge";
                _logger?.LogWarning(
                    "Convergent catch-up aborted for {ProjectionType}: {Reason}",
                    typeof(T).Name, reason);
                return ConvergentCatchUpResult.Failed(iteration, totalEventsApplied, sw.Elapsed, reason);
            }

            var syncResult = await SyncAsync<T>(objectId, sourceVersion, targetVersion, cancellationToken);
            totalEventsApplied += eventsInIteration;

            if (syncResult.IsSynced)
            {
                sw.Stop();
                return ConvergentCatchUpResult.Success(iteration, totalEventsApplied, sw.Elapsed);
            }

            if (iteration < options.MaxIterations)
            {
                await Task.Delay(options.IterationDelay, cancellationToken);
            }
        }

        sw.Stop();
        var failureReason = $"Max iterations ({options.MaxIterations}) reached without convergence";
        _logger?.LogWarning(
            "Convergent catch-up failed for {ProjectionType}: {Reason}",
            typeof(T).Name, failureReason);
        return ConvergentCatchUpResult.Failed(options.MaxIterations, totalEventsApplied, sw.Elapsed, failureReason);
    }

    private static CheckpointComparisonResult CompareCheckpoints(Projection source, Projection target)
    {
        var sourceFingerprint = source.CheckpointFingerprint;
        var targetFingerprint = target.CheckpointFingerprint;

        // Fast path: fingerprints match
        if (sourceFingerprint != null && sourceFingerprint == targetFingerprint)
        {
            return CheckpointComparisonResult.Synced(sourceFingerprint);
        }

        var streamDiffs = new List<StreamDiff>();
        var missingStreams = new List<string>();
        var totalMissing = 0;

        foreach (var (objectId, sourceVersionId) in source.Checkpoint)
        {
            var streamIdStr = objectId.Value;

            if (!target.Checkpoint.TryGetValue(objectId, out var targetVersionId))
            {
                missingStreams.Add(streamIdStr);
                totalMissing++;
            }
            else if (sourceVersionId != targetVersionId)
            {
                var estimated = EstimateMissingEvents(sourceVersionId, targetVersionId);
                streamDiffs.Add(new StreamDiff(
                    streamIdStr,
                    sourceVersionId.Value,
                    targetVersionId.Value,
                    estimated));
                totalMissing += estimated;
            }
        }

        if (streamDiffs.Count == 0 && missingStreams.Count == 0)
        {
            // Fingerprints differed but actual checkpoint entries match — treat as synced
            return CheckpointComparisonResult.Synced(sourceFingerprint ?? "");
        }

        var diff = new CheckpointDiff(streamDiffs, totalMissing, missingStreams);
        return CheckpointComparisonResult.Different(
            sourceFingerprint ?? "",
            targetFingerprint ?? "",
            diff);
    }

    private static int EstimateMissingEvents(VersionIdentifier sourceId, VersionIdentifier targetId)
    {
        // Try to extract version numbers from version strings for estimation
        if (int.TryParse(sourceId.VersionString, out var sourceVer) &&
            int.TryParse(targetId.VersionString, out var targetVer))
        {
            return Math.Max(0, sourceVer - targetVer);
        }

        // Can't estimate, return 1 as minimum
        return 1;
    }

    private static string GetVersionedBlobName<T>(int version) where T : Projection
    {
        var name = typeof(T).Name;
        return $"{name}_v{version}";
    }
}
