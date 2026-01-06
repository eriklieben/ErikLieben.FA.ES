using System.Diagnostics;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles SprintDashboard projection updates
/// Processes sprint events to maintain a dashboard of all sprints
/// </summary>
public class SprintDashboardProjectionHandler(
    IProjectionService projectionService,
    SprintDashboardFactory factory,
    ILogger<SprintDashboardProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;

    public string ProjectionName => "SprintDashboard";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var relevantEvents = events
            .Where(e => e.ObjectName == "sprint")
            .ToList();

        if (relevantEvents.Count == 0)
        {
            logger.LogDebug("No sprint events in batch, skipping SprintDashboard update");
            return;
        }

        var projection = projectionService.GetSprintDashboard();
        if (projection == null)
        {
            logger.LogWarning("SprintDashboard projection not available - projectionService.GetSprintDashboard() returned null");
            return;
        }

        logger.LogInformation(
            "[SprintDashboard] Processing {EventCount} sprint events. Before: {SprintCount} sprints, {CheckpointCount} checkpoint entries",
            relevantEvents.Count,
            projection.Sprints.Count,
            projection.Checkpoint.Count);

        var stopwatch = Stopwatch.StartNew();

        foreach (var evt in relevantEvents)
        {
            logger.LogInformation(
                "[SprintDashboard] UpdateToVersion: ObjectName={ObjectName}, ObjectId={ObjectId}, Version={Version}",
                evt.VersionToken.ObjectName,
                evt.VersionToken.ObjectId,
                evt.VersionToken.Version);

            await projection.UpdateToVersion(evt.VersionToken);

            logger.LogInformation(
                "[SprintDashboard] After UpdateToVersion: {SprintCount} sprints, {CheckpointCount} checkpoint entries",
                projection.Sprints.Count,
                projection.Checkpoint.Count);
        }

        await factory.SaveAsync(projection, cancellationToken: cancellationToken);

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
        var projection = projectionService.GetSprintDashboard();
        var lastModified = await factory.GetLastModifiedAsync();

        return new ProjectionStatus
        {
            Name = ProjectionName,
            CheckpointCount = projection?.Checkpoint.Count ?? 0,
            CheckpointFingerprint = projection?.CheckpointFingerprint,
            LastModified = lastModified,
            IsPersisted = lastModified.HasValue,
            LastGenerationDurationMs = _lastGenerationDurationMs
        };
    }
}
