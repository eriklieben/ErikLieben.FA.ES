using System.Diagnostics;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles ProjectDashboard projection updates
/// Filters and processes both project and workItem events (cross-aggregate projection)
/// </summary>
public class ProjectDashboardProjectionHandler(
    IProjectionService projectionService,
    ProjectDashboardFactory factory,
    ILogger<ProjectDashboardProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;

    public string ProjectionName => "ProjectDashboard";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var relevantEvents = events
            .Where(e => e.ObjectName == "project" || e.ObjectName == "workitem")
            .ToList();

        if (relevantEvents.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "Processing {EventCount} project/workItem events for {ProjectionName}",
            relevantEvents.Count,
            ProjectionName);

        var stopwatch = Stopwatch.StartNew();

        var projection = projectionService.GetProjectDashboard();

        foreach (var evt in relevantEvents)
        {
            await projection.UpdateToVersion(evt.VersionToken);
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
        var projection = projectionService.GetProjectDashboard();
        var lastModified = await factory.GetLastModifiedAsync();

        return new ProjectionStatus
        {
            Name = ProjectionName,
            CheckpointCount = projection.Checkpoint.Count,
            CheckpointFingerprint = projection.CheckpointFingerprint,
            LastModified = lastModified,
            IsPersisted = lastModified.HasValue,
            LastGenerationDurationMs = _lastGenerationDurationMs
        };
    }
}
