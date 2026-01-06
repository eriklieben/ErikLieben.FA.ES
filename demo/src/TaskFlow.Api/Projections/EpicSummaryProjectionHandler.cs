using System.Diagnostics;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles EpicSummary projection updates
/// Processes epic events to maintain a list of all epics
/// </summary>
public class EpicSummaryProjectionHandler(
    IProjectionService projectionService,
    EpicSummaryFactory factory,
    ILogger<EpicSummaryProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;

    public string ProjectionName => "EpicSummary";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var relevantEvents = events
            .Where(e => e.ObjectName == "epic")
            .ToList();

        if (relevantEvents.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "Processing {EventCount} epic events for {ProjectionName}",
            relevantEvents.Count,
            ProjectionName);

        var stopwatch = Stopwatch.StartNew();

        var projection = projectionService.GetEpicSummary();

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
        var projection = projectionService.GetEpicSummary();
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
