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
            _logger.LogInformation(
                "Starting live migration {MigrationId} from {SourceStream} to {TargetStream}",
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
                    _logger.LogDebug(
                        "Iteration {Iteration}: Target at {TargetVersion}, source at {SourceVersion}. Continuing catch-up.",
                        _iteration, targetVersion, sourceVersion);

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

                    _logger.LogInformation(
                        "Live migration {MigrationId} completed successfully. Copied {EventCount} events in {Iterations} iterations ({Elapsed})",
                        _context.MigrationId,
                        _totalEventsCopied,
                        _iteration,
                        _stopwatch.Elapsed);

                    return CreateSuccessResult();
                }

                // Close failed due to version conflict - new events arrived
                _logger.LogDebug(
                    "Iteration {Iteration}: Close attempt failed. Source version changed from {Expected} to {Actual}. Retrying catch-up.",
                    _iteration, sourceVersion, closeResult.ActualVersion);

                await Task.Delay(_context.Options.CatchUpDelay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return CreateFailureResult("Migration was cancelled");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Live migration {MigrationId} was cancelled", _context.MigrationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live migration {MigrationId} failed", _context.MigrationId);
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
        var existingTarget = await _context.DocumentStore.GetAsync(
            _context.SourceDocument.ObjectName,
            _context.SourceDocument.ObjectId + "_migration_target_" + _context.TargetStreamId);

        if (existingTarget == null)
        {
            _logger.LogDebug("Target stream {TargetStream} will be created on first event write", _context.TargetStreamId);
        }
    }

    private async Task<int> CatchUpAsync(CancellationToken cancellationToken)
    {
        // Get current source version
        var sourceEvents = await _context.DataStore.ReadAsync(
            _context.SourceDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null);

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

        // Apply transformations if configured
        var transformedEvents = new List<IEvent>();
        foreach (var evt in eventsToCopy)
        {
            IEvent transformedEvent = evt;

            if (_context.Transformer != null)
            {
                try
                {
                    transformedEvent = await _context.Transformer.TransformAsync(evt, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to transform event {EventType} v{Version}. Skipping.",
                        evt.EventType, evt.EventVersion);
                    continue;
                }
            }

            transformedEvents.Add(transformedEvent);
        }

        // Write events to target stream
        if (transformedEvents.Count > 0)
        {
            await _context.DataStore.AppendAsync(
                _context.TargetDocument,
                preserveTimestamp: true,
                transformedEvents.ToArray());

            _totalEventsCopied += transformedEvents.Count;

            _logger.LogDebug(
                "Copied {EventCount} events to target stream (versions {Start} to {End})",
                transformedEvents.Count,
                transformedEvents[0].EventVersion,
                transformedEvents[^1].EventVersion);
        }

        var newTargetVersion = targetVersion + transformedEvents.Count;
        ReportProgress(transformedEvents.Count, sourceVersion, newTargetVersion);

        return sourceVersion;
    }

    private async Task<int> GetTargetVersionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetEvents = await _context.DataStore.ReadAsync(
            _context.TargetDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null);

        if (targetEvents == null || !targetEvents.Any())
        {
            return -1;
        }

        return targetEvents.Max(e => e.EventVersion);
    }

    private async Task<CloseAttemptResult> AttemptCloseAsync(int expectedVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Re-read source to get current version
        var currentEvents = await _context.DataStore.ReadAsync(
            _context.SourceDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null);

        var currentEventList = currentEvents?
            .Where(e => e.EventType != StreamClosedEvent.EventTypeName)
            .ToList() ?? [];

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
            // Append the close event to the source stream
            await _context.DataStore.AppendAsync(
                _context.SourceDocument,
                preserveTimestamp: false,
                closeEventJson);

            _logger.LogInformation(
                "Successfully closed source stream {SourceStream} at version {Version}",
                _context.SourceStreamId,
                expectedVersion + 1);

            return CloseAttemptResult.Succeeded();
        }
        catch (OptimisticConcurrencyException ex)
        {
            _logger.LogDebug(ex, "Optimistic concurrency conflict during close attempt");
            return CloseAttemptResult.VersionConflict(ex.ActualVersion ?? actualVersion);
        }
        catch (Exception ex) when (IsVersionConflict(ex))
        {
            _logger.LogDebug(ex, "Version conflict during close attempt");

            // Re-read to get actual version
            var newEvents = await _context.DataStore.ReadAsync(
                _context.SourceDocument,
                startVersion: 0,
                untilVersion: null,
                chunk: null);

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

        _logger.LogInformation(
            "Updated ObjectDocument: Active stream is now {TargetStream}",
            _context.TargetStreamId);
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
