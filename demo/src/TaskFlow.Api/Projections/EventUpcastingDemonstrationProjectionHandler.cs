using System.Diagnostics;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles EventUpcastingDemonstration projection updates
/// Filters and processes only project events for specific tracked demo projects
/// </summary>
public class EventUpcastingDemonstrationProjectionHandler(
    IProjectionService projectionService,
    EventUpcastingDemonstrationFactory factory,
    ILogger<EventUpcastingDemonstrationProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;

    public string ProjectionName => "EventUpcastingDemonstration";

    /// <summary>
    /// Project IDs that this projection tracks for the upcasting demonstration
    /// </summary>
    private static readonly HashSet<string> TrackedProjectIds = [..DemoProjectIds.AllUpcastingDemoProjects];

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var relevantEvents = events
            .Where(e => e.ObjectName == "project" && TrackedProjectIds.Contains(e.VersionToken.ObjectId))
            .ToList();

        if (relevantEvents.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "Processing {EventCount} tracked project events for {ProjectionName}",
            relevantEvents.Count,
            ProjectionName);

        var stopwatch = Stopwatch.StartNew();

        var projection = projectionService.GetEventUpcastingDemonstration();

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
        var projection = projectionService.GetEventUpcastingDemonstration();
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
