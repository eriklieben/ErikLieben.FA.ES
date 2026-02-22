using System.Diagnostics;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles ReleaseDashboard projection updates.
/// Processes release events to maintain a dashboard of all releases.
/// </summary>
public class ReleaseDashboardProjectionHandler(
    ReleaseDashboard releaseDashboard,
    ReleaseDashboardFactory factory,
    ILogger<ReleaseDashboardProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;

    public string ProjectionName => "ReleaseDashboard";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var relevantEvents = events
            .Where(e => e.ObjectName == "release")
            .ToList();

        if (relevantEvents.Count == 0)
        {
            logger.LogDebug("No release events in batch, skipping ReleaseDashboard update");
            return;
        }

        logger.LogInformation(
            "[ReleaseDashboard] Processing {EventCount} release events. Before: {ReleaseCount} releases, {CheckpointCount} checkpoint entries",
            relevantEvents.Count,
            releaseDashboard.Releases.Count,
            releaseDashboard.Checkpoint.Count);

        var stopwatch = Stopwatch.StartNew();

        foreach (var evt in relevantEvents)
        {
            logger.LogInformation(
                "[ReleaseDashboard] UpdateToVersion: ObjectName={ObjectName}, ObjectId={ObjectId}, Version={Version}",
                evt.VersionToken.ObjectName,
                evt.VersionToken.ObjectId,
                evt.VersionToken.Version);

            await releaseDashboard.UpdateToVersion(evt.VersionToken);

            logger.LogInformation(
                "[ReleaseDashboard] After UpdateToVersion: {ReleaseCount} releases, {CheckpointCount} checkpoint entries",
                releaseDashboard.Releases.Count,
                releaseDashboard.Checkpoint.Count);
        }

        await factory.SaveAsync(releaseDashboard, cancellationToken: cancellationToken);

        stopwatch.Stop();
        _lastGenerationDurationMs = stopwatch.ElapsedMilliseconds;

        logger.LogInformation(
            "Successfully updated {ProjectionName} with {EventCount} events in {DurationMs}ms",
            ProjectionName,
            relevantEvents.Count,
            _lastGenerationDurationMs);
    }

    public async Task<ProjectionStatus> GetStatusAsync()
    {
        var lastModified = await factory.GetLastModifiedAsync();

        return new ProjectionStatus
        {
            Name = ProjectionName,
            CheckpointCount = releaseDashboard.Checkpoint.Count,
            CheckpointFingerprint = releaseDashboard.CheckpointFingerprint,
            LastModified = lastModified,
            IsPersisted = lastModified.HasValue,
            LastGenerationDurationMs = _lastGenerationDurationMs
        };
    }
}
