using System.Diagnostics;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles WorkItemAuditLog projection updates.
/// Processes workItem events and appends audit log entries to CosmosDB.
/// </summary>
public class WorkItemAuditLogProjectionHandler(
    WorkItemAuditLog projection,
    WorkItemAuditLogFactory factory,
    ILogger<WorkItemAuditLogProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;
    private int _totalDocumentsAppended;

    public string ProjectionName => "WorkItemAuditLog";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var allEvents = events.ToList();
        logger.LogInformation(
            "[WorkItemAuditLog] HandleBatchAsync called with {TotalEvents} events. ObjectNames: {ObjectNames}",
            allEvents.Count,
            string.Join(", ", allEvents.Select(e => e.ObjectName).Distinct()));

        var workItemEvents = allEvents.Where(e => e.ObjectName == "workitem").ToList();

        if (workItemEvents.Count == 0)
        {
            logger.LogInformation("[WorkItemAuditLog] No workitem events found in batch, skipping");
            return;
        }

        logger.LogInformation(
            "[WorkItemAuditLog] Processing {EventCount} workItem events for {ProjectionName}",
            workItemEvents.Count,
            ProjectionName);

        var stopwatch = Stopwatch.StartNew();

        foreach (var evt in workItemEvents)
        {
            logger.LogInformation(
                "[WorkItemAuditLog] UpdateToVersion: ObjectName={ObjectName}, ObjectId={ObjectId}, Version={Version}",
                evt.VersionToken.ObjectName,
                evt.VersionToken.ObjectId,
                evt.VersionToken.Version);

            var pendingBefore = projection.PendingDocumentCount;
            await projection.UpdateToVersion(evt.VersionToken);
            var pendingAfter = projection.PendingDocumentCount;

            logger.LogInformation(
                "[WorkItemAuditLog] After UpdateToVersion: PendingDocs before={Before}, after={After}, checkpoint count={CheckpointCount}",
                pendingBefore,
                pendingAfter,
                projection.Checkpoint.Count);
        }

        // Save pending documents to CosmosDB
        var pendingCount = projection.PendingDocumentCount;
        if (pendingCount > 0)
        {
            await factory.SaveAsync(projection, cancellationToken: cancellationToken);
            _totalDocumentsAppended += pendingCount;
        }

        stopwatch.Stop();
        _lastGenerationDurationMs = stopwatch.ElapsedMilliseconds;

        logger.LogInformation(
            "Successfully appended {DocumentCount} audit log entries for {ProjectionName} in {DurationMs}ms (total: {TotalDocuments})",
            pendingCount,
            ProjectionName,
            _lastGenerationDurationMs,
            _totalDocumentsAppended);
    }

    public Task<ProjectionStatus> GetStatusAsync()
    {
        return Task.FromResult(new ProjectionStatus
        {
            Name = ProjectionName,
            CheckpointCount = projection.Checkpoint.Count,
            CheckpointFingerprint = projection.CheckpointFingerprint,
            LastModified = null, // CosmosDB multi-doc doesn't track this the same way
            IsPersisted = _totalDocumentsAppended > 0,
            LastGenerationDurationMs = _lastGenerationDurationMs
        });
    }
}
