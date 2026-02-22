using System.Diagnostics;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles ActiveWorkItems projection updates
/// Filters and processes only workItem events
/// </summary>
public class ActiveWorkItemsProjectionHandler(
    IProjectionService projectionService,
    ActiveWorkItemsFactory factory,
    ILogger<ActiveWorkItemsProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;

    public string ProjectionName => "ActiveWorkItems";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var workItemEvents = events.Where(e => e.ObjectName == "workitem").ToList();

        if (workItemEvents.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "Processing {EventCount} workItem events for {ProjectionName}",
            workItemEvents.Count,
            ProjectionName);

        var stopwatch = Stopwatch.StartNew();

        var projection = projectionService.GetActiveWorkItems();

        foreach (var evt in workItemEvents)
        {
            await projection.UpdateToVersion(evt.VersionToken);
        }

        await factory.SaveAsync(projection, cancellationToken: cancellationToken);

        stopwatch.Stop();
        _lastGenerationDurationMs = stopwatch.ElapsedMilliseconds;

        logger.LogInformation(
            "Successfully updated {ProjectionName} with {EventCount} events in {DurationMs}ms",
            ProjectionName,
            workItemEvents.Count,
            _lastGenerationDurationMs);
    }

    public async Task<ProjectionStatus> GetStatusAsync()
    {
        var projection = projectionService.GetActiveWorkItems();
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
