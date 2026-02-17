using System.Diagnostics;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles TimeSheetDashboard projection updates.
/// Processes timesheet events to maintain a dashboard of all timesheets.
/// </summary>
public class TimeSheetDashboardProjectionHandler(
    TimeSheetDashboard timeSheetDashboard,
    TimeSheetDashboardFactory factory,
    ILogger<TimeSheetDashboardProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;

    public string ProjectionName => "TimeSheetDashboard";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var relevantEvents = events
            .Where(e => e.ObjectName == "timesheet")
            .ToList();

        if (relevantEvents.Count == 0)
        {
            logger.LogDebug("No timesheet events in batch, skipping TimeSheetDashboard update");
            return;
        }

        logger.LogInformation(
            "[TimeSheetDashboard] Processing {EventCount} timesheet events. Before: {SheetCount} timesheets",
            relevantEvents.Count,
            timeSheetDashboard.TimeSheets.Count);

        var stopwatch = Stopwatch.StartNew();

        foreach (var evt in relevantEvents)
        {
            await timeSheetDashboard.UpdateToVersion(evt.VersionToken);
        }

        await factory.SaveAsync(timeSheetDashboard, cancellationToken: cancellationToken);

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
            CheckpointCount = timeSheetDashboard.Checkpoint.Count,
            CheckpointFingerprint = timeSheetDashboard.CheckpointFingerprint,
            LastModified = lastModified,
            IsPersisted = lastModified.HasValue,
            LastGenerationDurationMs = _lastGenerationDurationMs
        };
    }
}
