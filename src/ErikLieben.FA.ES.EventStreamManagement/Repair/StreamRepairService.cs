using System.Diagnostics;
using System.Text.Json;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Events;
using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.EventStreamManagement.Repair;

/// <summary>
/// Default implementation of <see cref="IStreamRepairService"/> for repairing broken event streams.
/// </summary>
public class StreamRepairService : IStreamRepairService
{
    private readonly IDataStore dataStore;
    private readonly IDocumentStore documentStore;
    private readonly ILogger<StreamRepairService> logger;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.EventStreamManagement.Repair");

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamRepairService"/> class.
    /// </summary>
    /// <param name="dataStore">The data store for event operations.</param>
    /// <param name="documentStore">The document store for metadata operations.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public StreamRepairService(
        IDataStore dataStore,
        IDocumentStore documentStore,
        ILogger<StreamRepairService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(logger);

        this.dataStore = dataStore;
        this.documentStore = documentStore;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<StreamRepairResult> RepairBrokenStreamAsync(
        IObjectDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var activity = ActivitySource.StartActivity("StreamRepairService.RepairBrokenStream");
        activity?.SetTag("StreamId", document.Active.StreamIdentifier);
        activity?.SetTag("ObjectName", document.ObjectName);
        activity?.SetTag("ObjectId", document.ObjectId);

        if (!document.Active.IsBroken)
        {
            logger.LogWarning(
                "Attempted to repair stream {StreamId} that is not marked as broken",
                document.Active.StreamIdentifier);

            throw new InvalidOperationException(
                $"Stream '{document.Active.StreamIdentifier}' is not marked as broken. " +
                "Use the overload with explicit version range for manual repair scenarios.");
        }

        var brokenInfo = document.Active.BrokenInfo;
        if (brokenInfo == null)
        {
            logger.LogWarning(
                "Stream {StreamId} is marked as broken but has no BrokenInfo",
                document.Active.StreamIdentifier);

            throw new InvalidOperationException(
                $"Stream '{document.Active.StreamIdentifier}' is marked as broken but BrokenInfo is null. " +
                "Use the overload with explicit version range for manual repair scenarios.");
        }

        return await RepairBrokenStreamAsync(
            document,
            brokenInfo.OrphanedFromVersion,
            brokenInfo.OrphanedToVersion,
            brokenInfo.ErrorMessage,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StreamRepairResult> RepairBrokenStreamAsync(
        IObjectDocument document,
        int fromVersion,
        int toVersion,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var activity = ActivitySource.StartActivity("StreamRepairService.RepairBrokenStreamWithRange");
        activity?.SetTag("StreamId", document.Active.StreamIdentifier);
        activity?.SetTag("FromVersion", fromVersion);
        activity?.SetTag("ToVersion", toVersion);

        logger.LogInformation(
            "Attempting to repair stream {StreamId} by removing events {FromVersion}-{ToVersion}",
            document.Active.StreamIdentifier,
            fromVersion,
            toVersion);

        try
        {
            var eventsRemoved = await ((IDataStoreRecovery)dataStore).RemoveEventsForFailedCommitAsync(
                document,
                fromVersion,
                toVersion);

            logger.LogInformation(
                "Successfully removed {EventsRemoved} events from stream {StreamId} (versions {FromVersion}-{ToVersion})",
                eventsRemoved,
                document.Active.StreamIdentifier,
                fromVersion,
                toVersion);

            // Create rollback record for audit trail
            var rollbackRecord = new RollbackRecord
            {
                RolledBackAt = DateTimeOffset.UtcNow,
                FromVersion = fromVersion,
                ToVersion = toVersion,
                EventsRemoved = eventsRemoved,
                OriginalError = reason ?? document.Active.BrokenInfo?.ErrorMessage,
                OriginalExceptionType = document.Active.BrokenInfo?.ErrorMessage != null
                    ? "ManualRepair"
                    : null
            };

            // Update document state
            document.Active.IsBroken = false;
            document.Active.BrokenInfo = null;
            document.Active.RollbackHistory ??= [];
            document.Active.RollbackHistory.Add(rollbackRecord);

            // Persist the updated document
            await documentStore.SetAsync(document);

            activity?.SetTag("EventsRemoved", eventsRemoved);
            activity?.SetTag("Success", true);

            return StreamRepairResult.Succeeded(eventsRemoved, rollbackRecord);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to repair stream {StreamId}: {ErrorMessage}",
                document.Active.StreamIdentifier,
                ex.Message);

            activity?.SetTag("Success", false);
            activity?.SetTag("Error", ex.Message);

            return StreamRepairResult.Failed(
                $"Failed to remove orphaned events: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task AppendRollbackMarkerAsync(
        IObjectDocument document,
        RollbackRecord rollbackRecord,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(rollbackRecord);

        using var activity = ActivitySource.StartActivity("StreamRepairService.AppendRollbackMarker");
        activity?.SetTag("StreamId", document.Active.StreamIdentifier);
        activity?.SetTag("CorrelationId", correlationId);

        logger.LogInformation(
            "Appending rollback marker event to stream {StreamId} for versions {FromVersion}-{ToVersion}",
            document.Active.StreamIdentifier,
            rollbackRecord.FromVersion,
            rollbackRecord.ToVersion);

        var markerEvent = new EventsRolledBackEvent
        {
            FromVersion = rollbackRecord.FromVersion,
            ToVersion = rollbackRecord.ToVersion,
            EventsRemoved = rollbackRecord.EventsRemoved,
            RolledBackAt = rollbackRecord.RolledBackAt,
            Reason = RollbackReason.ManualRepair,
            OriginalErrorMessage = rollbackRecord.OriginalError,
            OriginalExceptionType = rollbackRecord.OriginalExceptionType,
            CorrelationId = correlationId
        };

        // Create a wrapper event that implements IEvent
        var streamEvent = new RollbackMarkerEvent(
            EventsRolledBackEvent.EventTypeName,
            document.Active.CurrentStreamVersion + 1,
            markerEvent);

        await dataStore.AppendAsync(document, default, streamEvent);

        // Update the document version
        document.Active.CurrentStreamVersion++;
        await documentStore.SetAsync(document);

        logger.LogInformation(
            "Rollback marker event appended to stream {StreamId} at version {Version}",
            document.Active.StreamIdentifier,
            document.Active.CurrentStreamVersion);
    }

    /// <inheritdoc />
    public Task<IEnumerable<IObjectDocument>> FindBrokenStreamsAsync(
        string? objectName = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("StreamRepairService.FindBrokenStreams");
        activity?.SetTag("ObjectName", objectName);

        // Note: This implementation requires a document store that supports querying.
        // The base IDocumentStore interface doesn't have a List/Query method.
        // Implementations should override this method or use a store-specific query.

        logger.LogWarning(
            "FindBrokenStreamsAsync is not supported with the default IDocumentStore. " +
            "Use a document store that supports querying or implement a custom IStreamRepairService.");

        throw new NotSupportedException(
            "Finding broken streams requires a document store that supports querying. " +
            "The base IDocumentStore interface only provides Get operations. " +
            "Use store-specific implementations (e.g., CosmosDB query, Table Storage query) " +
            "or provide a custom IStreamRepairService implementation.");
    }

    /// <summary>
    /// Internal event wrapper for the rollback marker.
    /// </summary>
    private sealed class RollbackMarkerEvent : IEvent
    {
        public string? Payload { get; }
        public string EventType { get; }
        public int EventVersion { get; }
        public int SchemaVersion => 1;
        public string? ExternalSequencer => null;
        public ActionMetadata? ActionMetadata => null;
        public Dictionary<string, string> Metadata { get; } = [];

        public RollbackMarkerEvent(string eventType, int version, EventsRolledBackEvent data)
        {
            EventType = eventType;
            EventVersion = version;
            Payload = JsonSerializer.Serialize(data, LiveMigrationJsonContext.Default.EventsRolledBackEvent);
        }
    }
}
