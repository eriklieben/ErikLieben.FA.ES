#pragma warning disable CS0618 // Type or member is obsolete - supporting legacy connection name properties during migration
#pragma warning disable S2139 // Exception handling - migration requires specific error recovery patterns
#pragma warning disable S1135 // TODO comments - tracked in project backlog

namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

/// <summary>
/// Executes migration operations following the saga pattern for distributed coordination.
/// </summary>
public class MigrationExecutor
{
    private readonly MigrationContext context;
    private readonly IDistributedLockProvider lockProvider;
    private readonly ILogger<MigrationExecutor> logger;
    private readonly MigrationProgressTracker progressTracker;
    private readonly MigrationStatistics statistics;

    // Backup handle for potential rollback
    private IBackupHandle? backupHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationExecutor"/> class.
    /// </summary>
    public MigrationExecutor(
        MigrationContext context,
        IDistributedLockProvider lockProvider,
        ILoggerFactory loggerFactory)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.lockProvider = lockProvider ?? throw new ArgumentNullException(nameof(lockProvider));
        this.logger = loggerFactory.CreateLogger<MigrationExecutor>();
        this.progressTracker = new MigrationProgressTracker(
            context.MigrationId,
            context.ProgressConfig,
            loggerFactory.CreateLogger<MigrationProgressTracker>());
        this.statistics = new MigrationStatistics
        {
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Executes the migration.
    /// </summary>
    public async Task<IMigrationResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            progressTracker.SetStatus(MigrationStatus.InProgress);

            // Handle dry run
            if (context.IsDryRun)
            {
                return await ExecuteDryRunAsync(cancellationToken);
            }

            // Execute actual migration with distributed lock
            return await ExecuteMigrationWithLockAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.MigrationFailed(context.MigrationId, ex);
            progressTracker.ReportFailed(ex);

            statistics.CompletedAt = DateTimeOffset.UtcNow;

            return MigrationResult.CreateFailure(
                context.MigrationId,
                progressTracker.GetProgress(),
                ex,
                statistics);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<IMigrationResult> ExecuteDryRunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.ExecutingDryRun(context.MigrationId);

        // Step 1: Analyze source stream
        var sourceEvents = await context.DataStore!.ReadAsync(
            context.SourceDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null);

        var eventList = sourceEvents?.ToList() ?? [];
        var eventCount = eventList.Count;

        // Calculate event type distribution
        var eventTypeDistribution = eventList
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        // Estimate size (rough estimate based on event count)
        var estimatedSizeBytes = eventCount * 1024L; // Assume ~1KB per event average

        var sourceAnalysis = new Verification.StreamAnalysis
        {
            EventCount = eventCount,
            SizeBytes = estimatedSizeBytes,
            EventTypeDistribution = eventTypeDistribution,
            CurrentVersion = context.SourceDocument.Active.CurrentStreamVersion
        };

        // Step 2: Simulate transformations if configured
        var transformationSimulation = new Verification.TransformationSimulation
        {
            SampleSize = 0,
            SuccessfulTransformations = 0,
            FailedTransformations = 0,
            Failures = []
        };

        if (context.Transformer != null && eventList.Count > 0)
        {
            var sampleSize = Math.Min(
                context.VerificationConfig?.TransformationSampleSize ?? 100,
                eventList.Count);

            transformationSimulation.SampleSize = sampleSize;

            // Sample events for transformation testing
            var sampleEvents = eventList.Take(sampleSize).ToList();

            foreach (var evt in sampleEvents)
            {
                try
                {
                    await context.Transformer.TransformAsync(evt, cancellationToken);
                    transformationSimulation.SuccessfulTransformations++;
                }
                catch (Exception ex)
                {
                    transformationSimulation.FailedTransformations++;
                    transformationSimulation.Failures.Add(new Verification.TransformationFailure
                    {
                        EventVersion = evt.EventVersion,
                        EventName = evt.EventType,
                        Error = ex.Message
                    });
                }
            }
        }

        // Step 3: Estimate resources
        var eventsPerSecond = 1000.0; // Conservative estimate
        var estimatedDuration = TimeSpan.FromSeconds(eventCount / eventsPerSecond);

        var resourceEstimate = new Verification.ResourceEstimate
        {
            EstimatedDuration = estimatedDuration,
            EstimatedStorageBytes = estimatedSizeBytes,
            EstimatedBandwidthBytes = estimatedSizeBytes * 2 // Read + Write
        };

        // Step 4: Check prerequisites
        var prerequisites = new List<Verification.Prerequisite>();

        if (context.DataStore == null)
        {
            prerequisites.Add(new Verification.Prerequisite
            {
                Name = "DataStore",
                IsMet = false,
                Description = "A data store must be configured for migration"
            });
        }
        else
        {
            prerequisites.Add(new Verification.Prerequisite
            {
                Name = "DataStore",
                IsMet = true,
                Description = "Data store is configured"
            });
        }

        if (context.DocumentStore == null)
        {
            prerequisites.Add(new Verification.Prerequisite
            {
                Name = "DocumentStore",
                IsMet = false,
                Description = "A document store must be configured for cutover"
            });
        }
        else
        {
            prerequisites.Add(new Verification.Prerequisite
            {
                Name = "DocumentStore",
                IsMet = true,
                Description = "Document store is configured"
            });
        }

        // Step 5: Identify risks
        var risks = new List<Verification.MigrationRisk>();

        if (eventCount > 10000)
        {
            risks.Add(new Verification.MigrationRisk
            {
                Category = "Performance",
                Severity = "Medium",
                Description = $"Large stream with {eventCount} events may take significant time",
                Mitigations = ["Consider running during off-peak hours", "Use chunked migration"]
            });
        }

        if (transformationSimulation.FailedTransformations > 0)
        {
            var failureRate = (double)transformationSimulation.FailedTransformations / transformationSimulation.SampleSize * 100;
            risks.Add(new Verification.MigrationRisk
            {
                Category = "Transformation",
                Severity = failureRate > 10 ? "High" : "Medium",
                Description = $"{transformationSimulation.FailedTransformations} transformation failures in sample ({failureRate:F1}%)",
                Mitigations = ["Review transformer implementation for edge cases"]
            });
        }

        if (context.BackupConfig == null)
        {
            risks.Add(new Verification.MigrationRisk
            {
                Category = "Data Safety",
                Severity = "High",
                Description = "No backup configured - data loss possible on failure",
                Mitigations = ["Configure backup before running migration"]
            });
        }

        // Step 6: Determine feasibility
        var allPrerequisitesMet = prerequisites.All(p => p.IsMet);
        var noHighRisks = !risks.Any(r => r.Severity == "High");
        var isFeasible = allPrerequisitesMet && (noHighRisks || context.BackupConfig != null);

        // Step 7: Recommend phases
        var recommendedPhases = new List<string>();
        if (context.BackupConfig != null)
        {
            recommendedPhases.Add("1. Create backup of source stream");
        }
        recommendedPhases.Add($"{(context.BackupConfig != null ? "2" : "1")}. Copy and transform {eventCount} events");
        if (context.VerificationConfig != null)
        {
            recommendedPhases.Add($"{recommendedPhases.Count + 1}. Verify migration integrity");
        }
        recommendedPhases.Add($"{recommendedPhases.Count + 1}. Perform cutover to target stream");
        if (context.BookClosingConfig != null)
        {
            recommendedPhases.Add($"{recommendedPhases.Count + 1}. Archive source stream");
        }

        var plan = new Verification.MigrationPlan
        {
            PlanId = Guid.NewGuid(),
            SourceAnalysis = sourceAnalysis,
            TransformationSimulation = transformationSimulation,
            ResourceEstimate = resourceEstimate,
            Prerequisites = prerequisites.ToArray(),
            Risks = risks.ToArray(),
            RecommendedPhases = recommendedPhases.ToArray(),
            IsFeasible = isFeasible
        };

        progressTracker.SetStatus(MigrationStatus.Completed);
        progressTracker.ReportCompleted();

        return MigrationResult.CreateDryRun(
            context.MigrationId,
            progressTracker.GetProgress(),
            plan);
    }

    private async Task<IMigrationResult> ExecuteMigrationWithLockAsync(CancellationToken cancellationToken)
    {
        // Acquire distributed lock if configured
        IDistributedLock? migrationLock = null;

        try
        {
            if (context.LockOptions != null)
            {
                logger.AcquiringLock(context.MigrationId);

                var lockKey = $"migration-{context.SourceDocument.ObjectId}";
                migrationLock = await lockProvider.AcquireLockAsync(
                    lockKey,
                    context.LockOptions.LockTimeoutValue,
                    cancellationToken);

                if (migrationLock == null)
                {
                    throw new MigrationException(
                        $"Failed to acquire distributed lock for {lockKey}. " +
                        "Another migration may be in progress for this object.");
                }

                logger.AcquiredLock(migrationLock.LockId, context.MigrationId);

                // Start heartbeat if configured
                if (context.LockOptions.HeartbeatIntervalValue > TimeSpan.Zero)
                {
                    StartHeartbeat(migrationLock, context.LockOptions.HeartbeatIntervalValue, cancellationToken);
                }
            }

            // Execute migration saga
            return await ExecuteMigrationSagaAsync(cancellationToken);
        }
        finally
        {
            // Release lock
            if (migrationLock != null)
            {
                await migrationLock.DisposeAsync();
            }
        }
    }

    private async Task<IMigrationResult> ExecuteMigrationSagaAsync(CancellationToken cancellationToken)
    {
        logger.ExecutingMigrationSaga(context.MigrationId);

        try
        {
            // Step 1: Create backup (if configured)
            if (context.BackupConfig != null && context.BackupProvider != null)
            {
                logger.StepCreatingBackup();
                progressTracker.SetStatus(MigrationStatus.BackingUp);

                // Read all events for backup
                var eventsToBackup = await context.DataStore!.ReadAsync(
                    context.SourceDocument,
                    startVersion: 0,
                    untilVersion: null,
                    chunk: null);

                var backupContext = new BackupContext
                {
                    Document = context.SourceDocument,
                    Configuration = context.BackupConfig,
                    Events = eventsToBackup
                };

                backupHandle = await context.BackupProvider.BackupAsync(
                    backupContext,
                    progress: null,
                    cancellationToken);

                logger.BackupCreated(backupHandle.BackupId);
            }

            // Step 2: Analyze source stream
            logger.StepAnalyzingSourceStream();
            var eventCount = await AnalyzeSourceStreamAsync(cancellationToken);
            progressTracker.TotalEvents = eventCount;

            // Step 3: Copy and transform events
            logger.StepCopyingEvents();
            await CopyAndTransformEventsAsync(cancellationToken);

            // Step 4: Verify migration (if configured)
            if (context.VerificationConfig != null)
            {
                logger.StepVerifyingMigration();
                progressTracker.SetStatus(MigrationStatus.Verifying);
                // TODO: Implement verification
            }

            // Step 5: Cutover to new stream
            logger.StepPerformingCutover();
            progressTracker.SetStatus(MigrationStatus.CuttingOver);
            await PerformCutoverAsync(cancellationToken);

            // Step 6: Book closing (if configured)
            if (context.BookClosingConfig != null)
            {
                logger.StepClosingBooks();
                // TODO: Implement book closing
            }

            statistics.CompletedAt = DateTimeOffset.UtcNow;
            progressTracker.ReportCompleted();

            return MigrationResult.CreateSuccess(
                context.MigrationId,
                progressTracker.GetProgress(),
                statistics);
        }
        catch (Exception ex)
        {
            logger.MigrationSagaFailed(ex);

            // Rollback if supported
            if (context.SupportsRollback)
            {
                logger.RollingBackMigration();
                progressTracker.SetStatus(MigrationStatus.RollingBack);
                // TODO: Implement rollback
            }

            throw;
        }
    }

    private async Task<long> AnalyzeSourceStreamAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read all events from source stream to count them
        var events = await context.DataStore!.ReadAsync(
            context.SourceDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null);

        var count = events?.Count() ?? 0;

        logger.SourceStreamAnalyzed(context.SourceStreamIdentifier, count);

        return count;
    }

    private async Task CopyAndTransformEventsAsync(CancellationToken cancellationToken)
    {
        // Read events from source
        var sourceEvents = await context.DataStore!.ReadAsync(
            context.SourceDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null);

        if (sourceEvents == null || !sourceEvents.Any())
        {
            logger.NoEventsInSourceStream();
            return;
        }

        var transformer = context.Transformer;
        var targetEvents = new List<IEvent>();

        foreach (var sourceEvent in sourceEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEvent targetEvent = sourceEvent;

            // Apply transformation if configured
            if (transformer != null)
            {
                try
                {
                    targetEvent = await transformer.TransformAsync(sourceEvent, cancellationToken);
                    statistics.EventsTransformed++;
                }
                catch (Exception ex)
                {
                    logger.TransformEventFailed(sourceEvent.EventType, sourceEvent.EventVersion, ex);

                    statistics.TransformationFailures++;

                    if (context.VerificationConfig?.FailFast == true)
                    {
                        throw;
                    }

                    // Skip this event on transformation failure
                    continue;
                }
            }

            targetEvents.Add(targetEvent);
            progressTracker.IncrementProcessed();
            statistics.TotalEvents++;
        }

        // Write to target stream using a temporary document with the target stream identifier
        if (targetEvents.Count > 0)
        {
            // Create target stream info based on source, but with new identifier
            var targetStreamInfo = new StreamInformation
            {
                StreamIdentifier = context.TargetStreamIdentifier,
                StreamType = context.SourceDocument.Active.StreamType,
                DocumentTagType = context.SourceDocument.Active.DocumentTagType,
                CurrentStreamVersion = -1,
                StreamConnectionName = context.SourceDocument.Active.StreamConnectionName,
                DocumentTagConnectionName = context.SourceDocument.Active.DocumentTagConnectionName,
                StreamTagConnectionName = context.SourceDocument.Active.StreamTagConnectionName,
                SnapShotConnectionName = context.SourceDocument.Active.SnapShotConnectionName,
                ChunkSettings = context.SourceDocument.Active.ChunkSettings,
                StreamChunks = [],
                SnapShots = [],
                DocumentType = context.SourceDocument.Active.DocumentType,
                EventStreamTagType = context.SourceDocument.Active.EventStreamTagType,
                DocumentRefType = context.SourceDocument.Active.DocumentRefType,
                DataStore = context.SourceDocument.Active.DataStore,
                DocumentStore = context.SourceDocument.Active.DocumentStore,
                DocumentConnectionName = context.SourceDocument.Active.DocumentConnectionName,
                DocumentTagStore = context.SourceDocument.Active.DocumentTagStore,
                StreamTagStore = context.SourceDocument.Active.StreamTagStore,
                SnapShotStore = context.SourceDocument.Active.SnapShotStore
            };

            // Create a temporary target document for writing events
            var targetDocument = new MigrationTargetDocument(
                context.SourceDocument.ObjectId,
                context.SourceDocument.ObjectName,
                targetStreamInfo);

            // Write all events to the target stream
            await context.DataStore.AppendAsync(targetDocument, targetEvents.ToArray());

            logger.WroteEventsToTarget(targetEvents.Count, context.TargetStreamIdentifier);
        }

        // Calculate average throughput
        statistics.AverageEventsPerSecond = progressTracker.GetProgress().EventsPerSecond;
    }

    private async Task PerformCutoverAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.DocumentStore == null)
        {
            throw new MigrationException("DocumentStore is required for cutover");
        }

        // Create a terminated stream entry for the source stream
        var terminatedStream = new TerminatedStream
        {
            StreamIdentifier = context.SourceStreamIdentifier,
            StreamType = context.SourceDocument.Active.StreamType,
            StreamConnectionName = context.SourceDocument.Active.StreamConnectionName,
            Reason = $"Migrated to {context.TargetStreamIdentifier}",
            ContinuationStreamId = context.TargetStreamIdentifier,
            TerminationDate = DateTimeOffset.UtcNow,
            StreamVersion = context.SourceDocument.Active.CurrentStreamVersion,
            Deleted = false
        };

        // Create new active stream information pointing to the target
        var newActiveStream = new StreamInformation
        {
            StreamIdentifier = context.TargetStreamIdentifier,
            StreamType = context.SourceDocument.Active.StreamType,
            DocumentTagType = context.SourceDocument.Active.DocumentTagType,
            CurrentStreamVersion = context.SourceDocument.Active.CurrentStreamVersion,
            StreamConnectionName = context.SourceDocument.Active.StreamConnectionName,
            DocumentTagConnectionName = context.SourceDocument.Active.DocumentTagConnectionName,
            StreamTagConnectionName = context.SourceDocument.Active.StreamTagConnectionName,
            SnapShotConnectionName = context.SourceDocument.Active.SnapShotConnectionName,
            ChunkSettings = context.SourceDocument.Active.ChunkSettings,
            StreamChunks = [],
            SnapShots = [],
            DocumentType = context.SourceDocument.Active.DocumentType,
            EventStreamTagType = context.SourceDocument.Active.EventStreamTagType,
            DocumentRefType = context.SourceDocument.Active.DocumentRefType,
            DataStore = context.SourceDocument.Active.DataStore,
            DocumentStore = context.SourceDocument.Active.DocumentStore,
            DocumentConnectionName = context.SourceDocument.Active.DocumentConnectionName,
            DocumentTagStore = context.SourceDocument.Active.DocumentTagStore,
            StreamTagStore = context.SourceDocument.Active.StreamTagStore,
            SnapShotStore = context.SourceDocument.Active.SnapShotStore
        };

        // Build the new terminated streams list
        var newTerminatedStreams = context.SourceDocument.TerminatedStreams.ToList();
        newTerminatedStreams.Add(terminatedStream);

        // Create an updated document with the new active stream and terminated streams
        var updatedDocument = new MigrationCutoverDocument(
            context.SourceDocument.ObjectId,
            context.SourceDocument.ObjectName,
            newActiveStream,
            newTerminatedStreams,
            context.SourceDocument.SchemaVersion,
            context.SourceDocument.Hash,
            context.SourceDocument.PrevHash);

        // Save the updated document
        await context.DocumentStore.SetAsync(updatedDocument);

        logger.CutoverComplete(context.SourceStreamIdentifier, context.TargetStreamIdentifier);
    }

    private void StartHeartbeat(
        IDistributedLock lockToRenew,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken);

                    if (!await lockToRenew.RenewAsync(cancellationToken))
                    {
                        logger.LockRenewalFailed(lockToRenew.LockId);
                        break;
                    }

                    logger.LockRenewed(lockToRenew.LockId);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.HeartbeatError(ex);
                }
            }
        }, cancellationToken);
    }
}

/// <summary>
/// A minimal IObjectDocument implementation used during migration to write events to the target stream.
/// </summary>
internal class MigrationTargetDocument : IObjectDocument
{
    public MigrationTargetDocument(
        string objectId,
        string objectName,
        StreamInformation active)
    {
        ObjectId = objectId;
        ObjectName = objectName;
        Active = active;
        TerminatedStreams = new List<TerminatedStream>();
    }

    public StreamInformation Active { get; }
    public string ObjectId { get; }
    public string ObjectName { get; }
    public List<TerminatedStream> TerminatedStreams { get; }
    public string? SchemaVersion { get; } = null;
    public string? Hash { get; private set; }
    public string? PrevHash { get; private set; }

    public void SetHash(string? hash, string? prevHash = null)
    {
        Hash = hash;
        PrevHash = prevHash;
    }
}

/// <summary>
/// An IObjectDocument implementation used during cutover to update the document with new active stream.
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
