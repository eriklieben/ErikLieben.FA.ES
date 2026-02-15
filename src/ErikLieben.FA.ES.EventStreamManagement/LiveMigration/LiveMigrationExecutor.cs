#pragma warning disable CS0618 // Type or member is obsolete - supporting legacy connection name properties during migration
#pragma warning disable S2139 // Exception handling - migration requires specific error recovery patterns

namespace ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

using System.Diagnostics;
using System.Text.Json;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Events;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Executes live migrations where the source stream remains active during the migration.
/// Uses a catch-up loop with optimistic concurrency to ensure atomic closure.
/// </summary>
public class LiveMigrationExecutor
{
    private readonly LiveMigrationContext _context;
    private readonly ILogger<LiveMigrationExecutor> _logger;
    private readonly Stopwatch _stopwatch = new();
    private long _totalEventsCopied;
    private int _iteration;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveMigrationExecutor"/> class.
    /// </summary>
    /// <param name="context">The live migration context.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public LiveMigrationExecutor(
        LiveMigrationContext context,
        ILoggerFactory loggerFactory)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<LiveMigrationExecutor>();
    }

    /// <summary>
    /// Executes the live migration.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The migration result.</returns>
    public async Task<LiveMigrationResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _stopwatch.Start();

        try
        {
            _logger.LiveMigrationStarted(
                _context.MigrationId,
                _context.SourceStreamId,
                _context.TargetStreamId);

            // Ensure target stream exists by creating the target document if needed
            await EnsureTargetStreamExistsAsync(cancellationToken);

            // Main catch-up loop
            while (!cancellationToken.IsCancellationRequested)
            {
                _iteration++;

                // Check iteration limit
                if (_context.Options.MaxIterations > 0 && _iteration > _context.Options.MaxIterations)
                {
                    return CreateFailureResult($"Exceeded maximum iterations ({_context.Options.MaxIterations})");
                }

                // Check timeout
                if (_stopwatch.Elapsed > _context.Options.CloseTimeout)
                {
                    return CreateFailureResult($"Close timeout exceeded ({_context.Options.CloseTimeout})");
                }

                // Phase 1: Catch-up - copy events from source to target
                var sourceVersion = await CatchUpAsync(cancellationToken);

                // Phase 2: Verify sync
                var targetVersion = await GetTargetVersionAsync(cancellationToken);

                if (targetVersion < sourceVersion)
                {
                    // Not yet synced, continue catch-up
                    _logger.LiveMigrationCatchUp(_iteration, targetVersion, sourceVersion);

                    await Task.Delay(_context.Options.CatchUpDelay, cancellationToken);
                    continue;
                }

                // Phase 3: Attempt atomic close
                var closeResult = await AttemptCloseAsync(sourceVersion, cancellationToken);

                if (closeResult.Success)
                {
                    // Phase 4: Update ObjectDocument
                    await UpdateObjectDocumentAsync(cancellationToken);

                    _stopwatch.Stop();

                    _logger.LiveMigrationSuccess(
                        _context.MigrationId,
                        _totalEventsCopied,
                        _iteration,
                        _stopwatch.Elapsed);

                    return CreateSuccessResult();
                }

                // Close failed due to version conflict - new events arrived
                _logger.LiveMigrationCloseRetry(_iteration, sourceVersion, closeResult.ActualVersion);

                await Task.Delay(_context.Options.CatchUpDelay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return CreateFailureResult("Migration was cancelled");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LiveMigrationCancelled(_context.MigrationId, ex);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LiveMigrationError(_context.MigrationId, ex);
            return CreateFailureResult(ex.Message, ex);
        }
        finally
        {
            _stopwatch.Stop();
        }
    }

    private async Task EnsureTargetStreamExistsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check if target document already exists
        // Note: Some document stores throw exceptions instead of returning null for non-existent documents
        try
        {
            var existingTarget = await _context.DocumentStore.GetAsync(
                _context.SourceDocument.ObjectName,
                _context.SourceDocument.ObjectId + "_migration_target_" + _context.TargetStreamId);

            if (existingTarget == null)
            {
                _logger.TargetStreamWillBeCreated(_context.TargetStreamId);
            }
        }
        catch (Exception ex) when (IsDocumentNotFoundException(ex))
        {
            // Document doesn't exist yet - that's fine, it will be created during the migration
            _logger.TargetStreamWillBeCreated(_context.TargetStreamId);
        }
    }

    private static bool IsDocumentNotFoundException(Exception ex)
    {
        // Check for common "not found" exception patterns across different document stores
        var typeName = ex.GetType().Name.ToLowerInvariant();
        var message = ex.Message.ToLowerInvariant();

        return typeName.Contains("notfound") ||
               message.Contains("not found") ||
               message.Contains("does not exist") ||
               message.Contains("404");
    }

    private async Task<int> CatchUpAsync(CancellationToken cancellationToken)
    {
        // Get current source version
        var sourceEvents = await _context.DataStore.ReadAsync(
            _context.SourceDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null,
            cancellationToken: cancellationToken);

        var sourceEventList = sourceEvents?.ToList() ?? [];

        // Filter out any existing close events (should not be copied)
        sourceEventList = sourceEventList
            .Where(e => e.EventType != StreamClosedEvent.EventTypeName)
            .ToList();

        var sourceVersion = sourceEventList.Count > 0
            ? sourceEventList.Max(e => e.EventVersion)
            : -1;

        // Get current target version
        var targetVersion = await GetTargetVersionAsync(cancellationToken);

        // Calculate events to copy
        var eventsToCopy = sourceEventList
            .Where(e => e.EventVersion > targetVersion)
            .OrderBy(e => e.EventVersion)
            .ToList();

        if (eventsToCopy.Count == 0)
        {
            ReportProgress(0, sourceVersion, targetVersion);
            return sourceVersion;
        }

        // Apply transformations and append events
        // When BeforeAppendCallback is set, append one at a time; otherwise batch
        var usePerEventAppend = _context.Options.BeforeAppendCallback != null;
        var transformedEvents = new List<IEvent>();
        var eventsAppendedCount = 0;

        foreach (var evt in eventsToCopy)
        {
            IEvent transformedEvent = evt;
            var wasTransformed = false;
            string? originalEventType = null;
            int? originalSchemaVersion = null;

            if (_context.Transformer != null)
            {
                try
                {
                    originalEventType = evt.EventType;
                    originalSchemaVersion = evt.SchemaVersion;
                    transformedEvent = await _context.Transformer.TransformAsync(evt, cancellationToken);
                    wasTransformed = transformedEvent.EventType != evt.EventType ||
                                     transformedEvent.SchemaVersion != evt.SchemaVersion;
                }
                catch (Exception ex)
                {
                    _logger.TransformEventSkipped(evt.EventType, evt.EventVersion, ex);
                    continue;
                }
            }

            // Create progress info for callbacks
            var progress = new LiveMigrationEventProgress
            {
                Iteration = _iteration,
                EventVersion = evt.EventVersion,
                EventType = transformedEvent.EventType,
                WasTransformed = wasTransformed,
                OriginalEventType = wasTransformed ? originalEventType : null,
                OriginalSchemaVersion = wasTransformed ? originalSchemaVersion : null,
                NewSchemaVersion = wasTransformed ? transformedEvent.SchemaVersion : null,
                TotalEventsCopied = _totalEventsCopied + eventsAppendedCount + 1,
                SourceVersion = sourceVersion,
                ElapsedTime = _stopwatch.Elapsed
            };

            if (usePerEventAppend)
            {
                // Per-event append mode: call before-append callback, then append immediately
                if (_context.Options.BeforeAppendCallback != null)
                {
                    await _context.Options.BeforeAppendCallback(progress);
                }

                await _context.DataStore.AppendAsync(
                    _context.TargetDocument,
                    preserveTimestamp: true,
                    cancellationToken: default,
                    transformedEvent);

                eventsAppendedCount++;
                _totalEventsCopied++;

                // Invoke per-event copied callback after append
                if (_context.Options.EventCopiedCallback != null)
                {
                    await _context.Options.EventCopiedCallback(progress);
                }

                _logger.EventsCopiedToTarget(1, evt.EventVersion, evt.EventVersion);
            }
            else
            {
                // Batch mode: collect events, invoke callback, append all at end
                if (_context.Options.EventCopiedCallback != null)
                {
                    await _context.Options.EventCopiedCallback(progress);
                }

                transformedEvents.Add(transformedEvent);
            }
        }

        // Write batched events to target stream (only in batch mode)
        if (!usePerEventAppend && transformedEvents.Count > 0)
        {
            await _context.DataStore.AppendAsync(
                _context.TargetDocument,
                preserveTimestamp: true,
                cancellationToken: default,
                transformedEvents.ToArray());

            _totalEventsCopied += transformedEvents.Count;
            eventsAppendedCount = transformedEvents.Count;

            _logger.EventsCopiedToTarget(
                transformedEvents.Count,
                transformedEvents[0].EventVersion,
                transformedEvents[^1].EventVersion);
        }

        var newTargetVersion = targetVersion + eventsAppendedCount;
        ReportProgress(eventsAppendedCount, sourceVersion, newTargetVersion);

        return sourceVersion;
    }

    private async Task<int> GetTargetVersionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var targetEvents = await _context.DataStore.ReadAsync(
                _context.TargetDocument,
                startVersion: 0,
                untilVersion: null,
                chunk: null,
                cancellationToken: cancellationToken);

            if (targetEvents == null || !targetEvents.Any())
            {
                return -1;
            }

            return targetEvents.Max(e => e.EventVersion);
        }
        catch (Exception ex) when (IsStreamNotFoundException(ex))
        {
            // Target stream doesn't exist yet - version is -1
            return -1;
        }
    }

    private static bool IsStreamNotFoundException(Exception ex)
    {
        // Check for common "not found" exception patterns across different data stores
        var typeName = ex.GetType().Name.ToLowerInvariant();
        var message = ex.Message.ToLowerInvariant();

        return typeName.Contains("notfound") ||
               message.Contains("not found") ||
               message.Contains("does not exist") ||
               message.Contains("404") ||
               message.Contains("blobnotfound");
    }

    private async Task<CloseAttemptResult> AttemptCloseAsync(int expectedVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Re-read source to get current version
        var currentEvents = await _context.DataStore.ReadAsync(
            _context.SourceDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null,
            cancellationToken: cancellationToken);

        var allEventsList = currentEvents?.ToList() ?? [];

        // Check if stream is already closed
        var existingCloseEvent = allEventsList.FirstOrDefault(e => e.EventType == StreamClosedEvent.EventTypeName);
        if (existingCloseEvent != null)
        {
            _logger.SourceStreamAlreadyClosed(_context.SourceStreamId);
            return CloseAttemptResult.Succeeded(); // Already closed - treat as success
        }

        var currentEventList = allEventsList
            .Where(e => e.EventType != StreamClosedEvent.EventTypeName)
            .ToList();

        var actualVersion = currentEventList.Count > 0
            ? currentEventList.Max(e => e.EventVersion)
            : -1;

        // Check if new events arrived
        if (actualVersion != expectedVersion)
        {
            return CloseAttemptResult.VersionConflict(actualVersion);
        }

        // Create the close event
        var closeEvent = new StreamClosedEvent
        {
            ContinuationStreamId = _context.TargetStreamId,
            ContinuationStreamType = _context.TargetDocument.Active.StreamType,
            ContinuationDataStore = _context.TargetDocument.Active.DataStore ?? string.Empty,
            ContinuationDocumentStore = _context.TargetDocument.Active.DocumentStore ?? string.Empty,
            Reason = StreamClosureReason.Migration,
            ClosedAt = DateTimeOffset.UtcNow,
            MigrationId = _context.MigrationId.ToString(),
            LastBusinessEventVersion = expectedVersion
        };

        // Serialize the close event to JSON
        var closeEventJson = new JsonEvent
        {
            EventType = StreamClosedEvent.EventTypeName,
            EventVersion = expectedVersion + 1,
            SchemaVersion = 1,
            Payload = JsonSerializer.Serialize(closeEvent, LiveMigrationJsonContext.Default.StreamClosedEvent)
        };

        try
        {
            // Reload the document to get the current hash - background processes (projections, etc.)
            // may have updated the document hash since we started the migration
            var freshDocument = await _context.DocumentStore.GetAsync(
                _context.SourceDocument.ObjectName,
                _context.SourceDocument.ObjectId);

            // Check if the document now points to a different stream (someone else completed migration)
            if (freshDocument.Active.StreamIdentifier != _context.SourceStreamId)
            {
                _logger.SourceStreamAlreadyClosed(_context.SourceStreamId);
                return CloseAttemptResult.Succeeded(); // Stream was already migrated by another process
            }

            // Re-read events after reload - new events may have arrived while we were reloading
            var eventsAfterReload = await _context.DataStore.ReadAsync(
                freshDocument,
                startVersion: 0,
                untilVersion: null,
                chunk: null,
                cancellationToken: cancellationToken);

            var eventsAfterReloadList = eventsAfterReload?.ToList() ?? [];

            // Check again if stream was closed while we were reloading
            var closeEventAfterReload = eventsAfterReloadList.FirstOrDefault(e => e.EventType == StreamClosedEvent.EventTypeName);
            if (closeEventAfterReload != null)
            {
                _logger.SourceStreamAlreadyClosed(_context.SourceStreamId);
                return CloseAttemptResult.Succeeded();
            }

            // Check if new business events arrived during reload
            var currentVersion = eventsAfterReloadList
                .Where(e => e.EventType != StreamClosedEvent.EventTypeName)
                .Select(e => e.EventVersion)
                .DefaultIfEmpty(-1)
                .Max();

            if (currentVersion != expectedVersion)
            {
                // New events arrived while reloading - need another catch-up iteration
                return CloseAttemptResult.VersionConflict(currentVersion);
            }

            // Append the close event to the source stream using the fresh document
            await _context.DataStore.AppendAsync(
                freshDocument,
                preserveTimestamp: false,
                cancellationToken: default,
                closeEventJson);

            _logger.SourceStreamClosed(_context.SourceStreamId, expectedVersion + 1);

            // Verify no events were added between our read and the close event append
            // This catches the race condition where events are added after version check
            var postCloseEvents = await _context.DataStore.ReadAsync(
                freshDocument,
                startVersion: 0,
                untilVersion: null,
                chunk: null,
                cancellationToken: cancellationToken);

            var businessEventsAfterClose = postCloseEvents?
                .Where(e => e.EventType != StreamClosedEvent.EventTypeName)
                .Where(e => e.EventVersion > expectedVersion)
                .ToList() ?? [];

            if (businessEventsAfterClose.Count > 0)
            {
                // Events were added during the close - we need to catch them up to target
                _logger.EventsAddedDuringClose(businessEventsAfterClose.Count, expectedVersion);

                // Copy the late events to target (close event is already on source)
                foreach (var lateEvent in businessEventsAfterClose.OrderBy(e => e.EventVersion))
                {
                    IEvent eventToCopy = lateEvent;

                    // Apply transformation if configured
                    if (_context.Transformer != null)
                    {
                        eventToCopy = await _context.Transformer.TransformAsync(lateEvent, cancellationToken);
                    }

                    await _context.DataStore.AppendAsync(
                        _context.TargetDocument,
                        preserveTimestamp: true,
                        cancellationToken: default,
                        eventToCopy);

                    _totalEventsCopied++;
                    _logger.EventsCopiedToTarget(1, lateEvent.EventVersion, lateEvent.EventVersion);
                }

                _logger.LateEventsCaughtUp(businessEventsAfterClose.Count);
            }

            return CloseAttemptResult.Succeeded();
        }
        catch (OptimisticConcurrencyException ex)
        {
            _logger.CloseAttemptConcurrencyConflict(ex);
            return CloseAttemptResult.VersionConflict(ex.ActualVersion ?? actualVersion);
        }
        catch (Exception ex) when (IsVersionConflict(ex))
        {
            _logger.CloseAttemptVersionConflict(ex);

            // Re-read to get actual version
            var newEvents = await _context.DataStore.ReadAsync(
                _context.SourceDocument,
                startVersion: 0,
                untilVersion: null,
                chunk: null,
                cancellationToken: cancellationToken);

            var newVersion = newEvents?.Max(e => e.EventVersion) ?? actualVersion;
            return CloseAttemptResult.VersionConflict(newVersion);
        }
    }

    private static bool IsVersionConflict(Exception ex)
    {
        // Check for common version conflict indicators
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("conflict") ||
               message.Contains("concurrency") ||
               message.Contains("version") ||
               message.Contains("etag");
    }

    private async Task UpdateObjectDocumentAsync(CancellationToken cancellationToken)
    {
        // Create a terminated stream entry for the source
        var terminatedStream = new TerminatedStream
        {
            StreamIdentifier = _context.SourceStreamId,
            StreamType = _context.SourceDocument.Active.StreamType,
            StreamConnectionName = _context.SourceDocument.Active.StreamConnectionName,
            Reason = $"Live migration to {_context.TargetStreamId}",
            ContinuationStreamId = _context.TargetStreamId,
            TerminationDate = DateTimeOffset.UtcNow,
            StreamVersion = _context.SourceDocument.Active.CurrentStreamVersion,
            Deleted = false
        };

        // Get the target version for the new active stream
        var targetVersion = await GetTargetVersionAsync(cancellationToken);

        // Create new active stream information pointing to target
        var newActiveStream = new StreamInformation
        {
            StreamIdentifier = _context.TargetStreamId,
            StreamType = _context.SourceDocument.Active.StreamType,
            DocumentTagType = _context.SourceDocument.Active.DocumentTagType,
            CurrentStreamVersion = targetVersion,
            StreamConnectionName = _context.SourceDocument.Active.StreamConnectionName,
            DocumentTagConnectionName = _context.SourceDocument.Active.DocumentTagConnectionName,
            StreamTagConnectionName = _context.SourceDocument.Active.StreamTagConnectionName,
            SnapShotConnectionName = _context.SourceDocument.Active.SnapShotConnectionName,
            ChunkSettings = _context.SourceDocument.Active.ChunkSettings,
            StreamChunks = [],
            SnapShots = [],
            DocumentType = _context.SourceDocument.Active.DocumentType,
            EventStreamTagType = _context.SourceDocument.Active.EventStreamTagType,
            DocumentRefType = _context.SourceDocument.Active.DocumentRefType,
            DataStore = _context.SourceDocument.Active.DataStore,
            DocumentStore = _context.SourceDocument.Active.DocumentStore,
            DocumentConnectionName = _context.SourceDocument.Active.DocumentConnectionName,
            DocumentTagStore = _context.SourceDocument.Active.DocumentTagStore,
            StreamTagStore = _context.SourceDocument.Active.StreamTagStore,
            SnapShotStore = _context.SourceDocument.Active.SnapShotStore
        };

        // Build updated terminated streams list
        var terminatedStreams = _context.SourceDocument.TerminatedStreams.ToList();
        terminatedStreams.Add(terminatedStream);

        // Create updated document
        var updatedDocument = new MigrationCutoverDocument(
            _context.SourceDocument.ObjectId,
            _context.SourceDocument.ObjectName,
            newActiveStream,
            terminatedStreams,
            _context.SourceDocument.SchemaVersion,
            _context.SourceDocument.Hash,
            _context.SourceDocument.PrevHash);

        // Save the updated document
        await _context.DocumentStore.SetAsync(updatedDocument);

        _logger.ObjectDocumentUpdated(_context.TargetStreamId);
    }

    private void ReportProgress(int eventsCopiedThisIteration, int sourceVersion, int targetVersion)
    {
        _context.Options.ProgressCallback?.Invoke(new LiveMigrationProgress
        {
            Iteration = _iteration,
            SourceVersion = sourceVersion,
            TargetVersion = targetVersion,
            EventsCopiedThisIteration = eventsCopiedThisIteration,
            TotalEventsCopied = _totalEventsCopied,
            ElapsedTime = _stopwatch.Elapsed
        });
    }

    private LiveMigrationResult CreateSuccessResult()
    {
        return new LiveMigrationResult
        {
            Success = true,
            MigrationId = _context.MigrationId,
            SourceStreamId = _context.SourceStreamId,
            TargetStreamId = _context.TargetStreamId,
            TotalEventsCopied = _totalEventsCopied,
            Iterations = _iteration,
            ElapsedTime = _stopwatch.Elapsed
        };
    }

    private LiveMigrationResult CreateFailureResult(string error, Exception? exception = null)
    {
        return new LiveMigrationResult
        {
            Success = false,
            MigrationId = _context.MigrationId,
            SourceStreamId = _context.SourceStreamId,
            TargetStreamId = _context.TargetStreamId,
            TotalEventsCopied = _totalEventsCopied,
            Iterations = _iteration,
            ElapsedTime = _stopwatch.Elapsed,
            Error = error,
            Exception = exception
        };
    }

    /// <summary>
    /// Result of a close attempt.
    /// </summary>
    private sealed record CloseAttemptResult
    {
        public bool Success { get; init; }
        public int ActualVersion { get; init; }

        public static CloseAttemptResult Succeeded() => new() { Success = true };
        public static CloseAttemptResult VersionConflict(int actualVersion) =>
            new() { Success = false, ActualVersion = actualVersion };
    }
}

/// <summary>
/// Internal document type used during cutover.
/// </summary>
internal class MigrationCutoverDocument : IObjectDocument
{
    public MigrationCutoverDocument(
        string objectId,
        string objectName,
        StreamInformation active,
        List<TerminatedStream> terminatedStreams,
        string? schemaVersion = null,
        string? hash = null,
        string? prevHash = null)
    {
        ObjectId = objectId;
        ObjectName = objectName;
        Active = active;
        TerminatedStreams = terminatedStreams;
        SchemaVersion = schemaVersion;
        Hash = hash;
        PrevHash = prevHash;
    }

    public StreamInformation Active { get; }
    public string ObjectId { get; }
    public string ObjectName { get; }
    public List<TerminatedStream> TerminatedStreams { get; }
    public string? SchemaVersion { get; }
    public string? Hash { get; private set; }
    public string? PrevHash { get; private set; }

    public void SetHash(string? hash, string? prevHash = null)
    {
        Hash = hash;
        PrevHash = prevHash;
    }
}
