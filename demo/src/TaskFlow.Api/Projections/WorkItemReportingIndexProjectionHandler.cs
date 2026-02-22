using System.Diagnostics;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles WorkItemReportingIndex projection updates.
/// Processes workItem events and upserts/deletes rows in Azure Table Storage.
/// </summary>
public class WorkItemReportingIndexProjectionHandler(
    WorkItemReportingIndex projection,
    WorkItemReportingIndexFactory factory,
    ILogger<WorkItemReportingIndexProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;
    private int _totalOperationsExecuted;

    public string ProjectionName => "WorkItemReportingIndex";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var allEvents = events.ToList();
        logger.LogInformation(
            "[WorkItemReportingIndex] HandleBatchAsync called with {TotalEvents} events. ObjectNames: {ObjectNames}",
            allEvents.Count,
            string.Join(", ", allEvents.Select(e => e.ObjectName).Distinct()));

        var workItemEvents = allEvents.Where(e => e.ObjectName == "workitem").ToList();

        if (workItemEvents.Count == 0)
        {
            logger.LogInformation("[WorkItemReportingIndex] No workitem events found in batch, skipping");
            return;
        }

        logger.LogInformation(
            "[WorkItemReportingIndex] Processing {EventCount} workItem events for {ProjectionName}",
            workItemEvents.Count,
            ProjectionName);

        var stopwatch = Stopwatch.StartNew();

        foreach (var evt in workItemEvents)
        {
            logger.LogInformation(
                "[WorkItemReportingIndex] UpdateToVersion: ObjectName={ObjectName}, ObjectId={ObjectId}, Version={Version}",
                evt.VersionToken.ObjectName,
                evt.VersionToken.ObjectId,
                evt.VersionToken.Version);

            var pendingBefore = projection.PendingOperationCount;
            await projection.UpdateToVersion(evt.VersionToken);
            var pendingAfter = projection.PendingOperationCount;

            logger.LogInformation(
                "[WorkItemReportingIndex] After UpdateToVersion: PendingOps before={Before}, after={After}, checkpoint count={CheckpointCount}",
                pendingBefore,
                pendingAfter,
                projection.Checkpoint.Count);
        }

        // Save pending operations to Table Storage
        var pendingCount = projection.PendingOperationCount;
        if (pendingCount > 0)
        {
            try
            {
                await factory.SaveAsync(projection, cancellationToken: cancellationToken);
                _totalOperationsExecuted += pendingCount;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[WorkItemReportingIndex] Failed to save {PendingCount} operations to Table Storage", pendingCount);
                throw;
            }
        }

        stopwatch.Stop();
        _lastGenerationDurationMs = stopwatch.ElapsedMilliseconds;

        logger.LogInformation(
            "Successfully executed {OperationCount} table operations for {ProjectionName} in {DurationMs}ms (total: {TotalOperations})",
            pendingCount,
            ProjectionName,
            _lastGenerationDurationMs,
            _totalOperationsExecuted);
    }

    public Task<ProjectionStatus> GetStatusAsync()
    {
        return Task.FromResult(new ProjectionStatus
        {
            Name = ProjectionName,
            CheckpointCount = projection.Checkpoint.Count,
            CheckpointFingerprint = projection.CheckpointFingerprint,
            LastModified = null, // Table Storage multi-row doesn't track this the same way
            IsPersisted = _totalOperationsExecuted > 0,
            LastGenerationDurationMs = _lastGenerationDurationMs
        });
    }
}
