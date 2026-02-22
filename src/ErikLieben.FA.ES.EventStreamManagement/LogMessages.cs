#pragma warning disable CS1591 // Missing XML comment - LoggerMessage source-generated methods are implementation details

namespace ErikLieben.FA.ES.EventStreamManagement;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging methods for EventStreamManagement.
/// Using LoggerMessage source generators for zero-allocation logging.
/// </summary>
public static partial class LogMessages
{
    // ===== Migration Executor =====

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Migration {MigrationId} failed")]
    public static partial void MigrationFailed(this ILogger logger, Guid migrationId, Exception exception);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Executing dry-run for migration {MigrationId}")]
    public static partial void ExecutingDryRun(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Acquiring distributed lock for migration {MigrationId}")]
    public static partial void AcquiringLock(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Acquired distributed lock {LockId} for migration {MigrationId}")]
    public static partial void AcquiredLock(this ILogger logger, string lockId, Guid migrationId);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Executing migration saga for {MigrationId}")]
    public static partial void ExecutingMigrationSaga(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Step 1: Creating backup")]
    public static partial void StepCreatingBackup(this ILogger logger);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Step 2: Analyzing source stream")]
    public static partial void StepAnalyzingSourceStream(this ILogger logger);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "Step 3: Copying and transforming events")]
    public static partial void StepCopyingEvents(this ILogger logger);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Step 4: Verifying migration")]
    public static partial void StepVerifyingMigration(this ILogger logger);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Step 5: Performing cutover")]
    public static partial void StepPerformingCutover(this ILogger logger);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Step 6: Closing books")]
    public static partial void StepClosingBooks(this ILogger logger);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Error,
        Message = "Migration saga failed")]
    public static partial void MigrationSagaFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Warning,
        Message = "Rolling back migration")]
    public static partial void RollingBackMigration(this ILogger logger);

    [LoggerMessage(
        EventId = 1014,
        Level = LogLevel.Information,
        Message = "Source stream {StreamId} contains {EventCount} events")]
    public static partial void SourceStreamAnalyzed(this ILogger logger, string streamId, long eventCount);

    [LoggerMessage(
        EventId = 1015,
        Level = LogLevel.Warning,
        Message = "No events found in source stream")]
    public static partial void NoEventsInSourceStream(this ILogger logger);

    [LoggerMessage(
        EventId = 1016,
        Level = LogLevel.Error,
        Message = "Failed to transform event {EventType} v{Version}")]
    public static partial void TransformEventFailed(this ILogger logger, string eventType, int version, Exception exception);

    [LoggerMessage(
        EventId = 1017,
        Level = LogLevel.Information,
        Message = "Wrote {EventCount} events to target stream {TargetStream}")]
    public static partial void WroteEventsToTarget(this ILogger logger, int eventCount, string targetStream);

    [LoggerMessage(
        EventId = 1018,
        Level = LogLevel.Information,
        Message = "Cutover complete: switched from {SourceStream} to {TargetStream}")]
    public static partial void CutoverComplete(this ILogger logger, string sourceStream, string targetStream);

    [LoggerMessage(
        EventId = 1019,
        Level = LogLevel.Warning,
        Message = "Failed to renew lock {LockId} - migration may be interrupted")]
    public static partial void LockRenewalFailed(this ILogger logger, string lockId);

    [LoggerMessage(
        EventId = 1020,
        Level = LogLevel.Debug,
        Message = "Renewed lock {LockId}")]
    public static partial void LockRenewed(this ILogger logger, string lockId);

    [LoggerMessage(
        EventId = 1021,
        Level = LogLevel.Error,
        Message = "Error in heartbeat loop")]
    public static partial void HeartbeatError(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1022,
        Level = LogLevel.Information,
        Message = "Backup created with ID {BackupId}")]
    public static partial void BackupCreated(this ILogger logger, Guid backupId);

    [LoggerMessage(
        EventId = 1023,
        Level = LogLevel.Information,
        Message = "Verification completed: {SuccessCount} passed, {FailureCount} failed")]
    public static partial void VerificationCompleted(this ILogger logger, int successCount, int failureCount);

    [LoggerMessage(
        EventId = 1024,
        Level = LogLevel.Information,
        Message = "Book closing completed for stream {StreamId}")]
    public static partial void BookClosingCompleted(this ILogger logger, string streamId);

    [LoggerMessage(
        EventId = 1025,
        Level = LogLevel.Information,
        Message = "Rollback initiated from backup {BackupId}")]
    public static partial void RollbackFromBackup(this ILogger logger, Guid backupId);

    // ===== Migration Builder =====

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Starting migration {MigrationId} from {SourceStream} to {TargetStream} (DryRun: {IsDryRun})")]
    public static partial void StartingMigration(this ILogger logger, Guid migrationId, string sourceStream, string targetStream, bool isDryRun);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Starting live migration {MigrationId} from {SourceStream} to {TargetStream}")]
    public static partial void StartingLiveMigration(this ILogger logger, Guid migrationId, string sourceStream, string targetStream);

    // ===== Migration Service =====

    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Debug,
        Message = "Creating migration builder for document {ObjectId}")]
    public static partial void CreatingMigrationBuilder(this ILogger logger, string objectId);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Debug,
        Message = "Creating bulk migration builder for {DocumentCount} documents")]
    public static partial void CreatingBulkMigrationBuilder(this ILogger logger, int documentCount);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Debug,
        Message = "Retrieved {Count} active migrations")]
    public static partial void RetrievedActiveMigrations(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Debug,
        Message = "Migration {MigrationId} not found in active migrations")]
    public static partial void MigrationNotFound(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Information,
        Message = "Paused migration {MigrationId}")]
    public static partial void PausedMigration(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1205,
        Level = LogLevel.Warning,
        Message = "Cannot pause migration {MigrationId} - not found")]
    public static partial void CannotPauseMigration(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1206,
        Level = LogLevel.Information,
        Message = "Resumed migration {MigrationId}")]
    public static partial void ResumedMigration(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1207,
        Level = LogLevel.Warning,
        Message = "Cannot resume migration {MigrationId} - not found")]
    public static partial void CannotResumeMigration(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1208,
        Level = LogLevel.Information,
        Message = "Cancelled migration {MigrationId}")]
    public static partial void CancelledMigration(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1209,
        Level = LogLevel.Warning,
        Message = "Cannot cancel migration {MigrationId} - not found")]
    public static partial void CannotCancelMigration(this ILogger logger, Guid migrationId);

    // ===== Progress Tracker =====

    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Information,
        Message = "Migration {MigrationId} progress: {Processed}/{Total} events ({Percentage:F1}%) - {EventsPerSecond:F1} events/sec")]
    public static partial void MigrationProgress(this ILogger logger, Guid migrationId, long processed, long total, double percentage, double eventsPerSecond);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Information,
        Message = "Migration {MigrationId} status changed to {Status}")]
    public static partial void MigrationStatusChanged(this ILogger logger, Guid migrationId, string status);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Information,
        Message = "Migration {MigrationId} phase changed to {Phase}")]
    public static partial void MigrationPhaseChanged(this ILogger logger, Guid migrationId, string phase);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Error,
        Message = "Migration {MigrationId} failed: {ErrorMessage}")]
    public static partial void MigrationProgressFailed(this ILogger logger, Guid migrationId, string errorMessage);

    [LoggerMessage(
        EventId = 1304,
        Level = LogLevel.Warning,
        Message = "Error collecting custom metric {MetricName}")]
    public static partial void CustomMetricError(this ILogger logger, string metricName, Exception exception);

    [LoggerMessage(
        EventId = 1305,
        Level = LogLevel.Information,
        Message = "Migration {MigrationId} completed in {Elapsed} ({EventCount} events at {Rate:F0} events/sec)")]
    public static partial void MigrationCompleted(this ILogger logger, Guid migrationId, TimeSpan elapsed, long eventCount, double rate);

    [LoggerMessage(
        EventId = 1306,
        Level = LogLevel.Error,
        Message = "Migration {MigrationId} failed after {Elapsed} ({EventCount} events processed)")]
    public static partial void MigrationFailedAfter(this ILogger logger, Guid migrationId, TimeSpan elapsed, long eventCount, Exception exception);

    // ===== Transformation Pipeline =====

    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Debug,
        Message = "Transforming event {EventType} v{Version}")]
    public static partial void TransformingEvent(this ILogger logger, string eventType, int version);

    [LoggerMessage(
        EventId = 1401,
        Level = LogLevel.Debug,
        Message = "Event {EventType} transformed successfully")]
    public static partial void EventTransformed(this ILogger logger, string eventType);

    // ===== Live Migration =====

    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Information,
        Message = "Live migration {MigrationId} iteration {Iteration}: copied {EventsCopied} events, {NewEvents} new events pending")]
    public static partial void LiveMigrationIteration(this ILogger logger, Guid migrationId, int iteration, long eventsCopied, int newEvents);

    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Information,
        Message = "Live migration {MigrationId} entering drain phase")]
    public static partial void LiveMigrationDrainPhase(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1502,
        Level = LogLevel.Information,
        Message = "Live migration {MigrationId} completed: {TotalEvents} events copied in {Duration}")]
    public static partial void LiveMigrationCompleted(this ILogger logger, Guid migrationId, long totalEvents, TimeSpan duration);

    [LoggerMessage(
        EventId = 1503,
        Level = LogLevel.Warning,
        Message = "Live migration {MigrationId} source stream closed during migration")]
    public static partial void LiveMigrationSourceClosed(this ILogger logger, Guid migrationId);

    [LoggerMessage(
        EventId = 1504,
        Level = LogLevel.Error,
        Message = "Live migration {MigrationId} failed after {Iterations} iterations")]
    public static partial void LiveMigrationFailed(this ILogger logger, Guid migrationId, int iterations, Exception exception);

    [LoggerMessage(
        EventId = 1505,
        Level = LogLevel.Information,
        Message = "Live migration {MigrationId} started from {SourceStream} to {TargetStream}")]
    public static partial void LiveMigrationStarted(this ILogger logger, Guid migrationId, string sourceStream, string targetStream);

    [LoggerMessage(
        EventId = 1506,
        Level = LogLevel.Debug,
        Message = "Iteration {Iteration}: Target at {TargetVersion}, source at {SourceVersion}. Continuing catch-up.")]
    public static partial void LiveMigrationCatchUp(this ILogger logger, int iteration, int targetVersion, int sourceVersion);

    [LoggerMessage(
        EventId = 1507,
        Level = LogLevel.Information,
        Message = "Live migration {MigrationId} completed successfully. Copied {EventCount} events in {Iterations} iterations ({Elapsed})")]
    public static partial void LiveMigrationSuccess(this ILogger logger, Guid migrationId, long eventCount, int iterations, TimeSpan elapsed);

    [LoggerMessage(
        EventId = 1508,
        Level = LogLevel.Debug,
        Message = "Iteration {Iteration}: Close attempt failed. Source version changed from {Expected} to {Actual}. Retrying catch-up.")]
    public static partial void LiveMigrationCloseRetry(this ILogger logger, int iteration, int expected, int actual);

    [LoggerMessage(
        EventId = 1509,
        Level = LogLevel.Warning,
        Message = "Live migration {MigrationId} was cancelled")]
    public static partial void LiveMigrationCancelled(this ILogger logger, Guid migrationId, Exception exception);

    [LoggerMessage(
        EventId = 1510,
        Level = LogLevel.Error,
        Message = "Live migration {MigrationId} failed")]
    public static partial void LiveMigrationError(this ILogger logger, Guid migrationId, Exception exception);

    [LoggerMessage(
        EventId = 1511,
        Level = LogLevel.Debug,
        Message = "Target stream {TargetStream} will be created on first event write")]
    public static partial void TargetStreamWillBeCreated(this ILogger logger, string targetStream);

    [LoggerMessage(
        EventId = 1512,
        Level = LogLevel.Warning,
        Message = "Failed to transform event {EventType} v{Version}. Skipping.")]
    public static partial void TransformEventSkipped(this ILogger logger, string eventType, int version, Exception exception);

    [LoggerMessage(
        EventId = 1513,
        Level = LogLevel.Debug,
        Message = "Copied {EventCount} events to target stream (versions {Start} to {End})")]
    public static partial void EventsCopiedToTarget(this ILogger logger, int eventCount, int start, int end);

    [LoggerMessage(
        EventId = 1514,
        Level = LogLevel.Information,
        Message = "Successfully closed source stream {SourceStream} at version {Version}")]
    public static partial void SourceStreamClosed(this ILogger logger, string sourceStream, int version);

    [LoggerMessage(
        EventId = 1515,
        Level = LogLevel.Debug,
        Message = "Optimistic concurrency conflict during close attempt")]
    public static partial void CloseAttemptConcurrencyConflict(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1516,
        Level = LogLevel.Debug,
        Message = "Version conflict during close attempt")]
    public static partial void CloseAttemptVersionConflict(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1517,
        Level = LogLevel.Information,
        Message = "Updated ObjectDocument: Active stream is now {TargetStream}")]
    public static partial void ObjectDocumentUpdated(this ILogger logger, string targetStream);

    [LoggerMessage(
        EventId = 1518,
        Level = LogLevel.Warning,
        Message = "{EventCount} events were added during close (after version {ExpectedVersion})")]
    public static partial void EventsAddedDuringClose(this ILogger logger, int eventCount, int expectedVersion);

    [LoggerMessage(
        EventId = 1519,
        Level = LogLevel.Information,
        Message = "Caught up {EventCount} late events to target stream")]
    public static partial void LateEventsCaughtUp(this ILogger logger, int eventCount);

    [LoggerMessage(
        EventId = 1520,
        Level = LogLevel.Information,
        Message = "Source stream {SourceStream} is already closed, skipping close")]
    public static partial void SourceStreamAlreadyClosed(this ILogger logger, string sourceStream);

    // ===== Transformation Pipeline =====

    [LoggerMessage(
        EventId = 1600,
        Level = LogLevel.Debug,
        Message = "Added transformer {TransformerType} to pipeline (Total: {Count})")]
    public static partial void TransformerAddedToPipeline(this ILogger logger, string transformerType, int count);

    [LoggerMessage(
        EventId = 1601,
        Level = LogLevel.Debug,
        Message = "Applying transformer {TransformerType} to event {EventType} v{Version}")]
    public static partial void ApplyingTransformer(this ILogger logger, string transformerType, string eventType, int version);
}
