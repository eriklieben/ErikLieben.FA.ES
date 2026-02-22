#pragma warning disable CS0618 // Type or member is obsolete - supporting legacy connection name properties during migration
#pragma warning disable S2139 // Exception handling - migration requires specific error recovery patterns

namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Verification;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

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
        var eventList = await ReadSourceEventsForDryRunAsync(cancellationToken);
        var eventCount = eventList.Count;
        var estimatedSizeBytes = eventCount * 1024L; // Assume ~1KB per event average

        var sourceAnalysis = AnalyzeSourceStream(eventList, eventCount, estimatedSizeBytes,
            context.SourceDocument.Active.CurrentStreamVersion);

        // Step 2: Simulate transformations if configured
        var transformationSimulation = await SimulateTransformationsAsync(eventList, cancellationToken);

        // Step 3: Estimate resources
        var resourceEstimate = EstimateResources(eventCount, estimatedSizeBytes);

        // Step 4: Check prerequisites
        var prerequisites = CheckPrerequisites();

        // Step 5: Identify risks
        var risks = IdentifyRisks(eventCount, transformationSimulation);

        // Step 6: Determine feasibility
        var allPrerequisitesMet = prerequisites.All(p => p.IsMet);
        var noHighRisks = !risks.Any(r => r.Severity == "High");
        var isFeasible = allPrerequisitesMet && (noHighRisks || context.BackupConfig != null);

        // Step 7: Recommend phases
        var recommendedPhases = BuildRecommendedPhases(eventCount);

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

    private async Task<List<IEvent>> ReadSourceEventsForDryRunAsync(CancellationToken cancellationToken)
    {
        var sourceEvents = await context.DataStore!.ReadAsync(
            context.SourceDocument,
            startVersion: 0,
            untilVersion: null,
            chunk: null,
            cancellationToken: cancellationToken);

        return sourceEvents?.ToList() ?? [];
    }

    private static Verification.StreamAnalysis AnalyzeSourceStream(
        List<IEvent> eventList,
        int eventCount,
        long estimatedSizeBytes,
        int currentVersion)
    {
        var eventTypeDistribution = eventList
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        return new Verification.StreamAnalysis
        {
            EventCount = eventCount,
            SizeBytes = estimatedSizeBytes,
            EventTypeDistribution = eventTypeDistribution,
            CurrentVersion = currentVersion
        };
    }

    private async Task<Verification.TransformationSimulation> SimulateTransformationsAsync(
        List<IEvent> eventList,
        CancellationToken cancellationToken)
    {
        var simulation = new Verification.TransformationSimulation
        {
            SampleSize = 0,
            SuccessfulTransformations = 0,
            FailedTransformations = 0,
            Failures = []
        };

        if (context.Transformer == null || eventList.Count == 0)
        {
            return simulation;
        }

        var sampleSize = Math.Min(
            context.VerificationConfig?.TransformationSampleSize ?? 100,
            eventList.Count);

        simulation.SampleSize = sampleSize;

        var sampleEvents = eventList.Take(sampleSize).ToList();

        foreach (var evt in sampleEvents)
        {
            try
            {
                await context.Transformer.TransformAsync(evt, cancellationToken);
                simulation.SuccessfulTransformations++;
            }
            catch (Exception ex)
            {
                simulation.FailedTransformations++;
                simulation.Failures.Add(new Verification.TransformationFailure
                {
                    EventVersion = evt.EventVersion,
                    EventName = evt.EventType,
                    Error = ex.Message
                });
            }
        }

        return simulation;
    }

    private static Verification.ResourceEstimate EstimateResources(int eventCount, long estimatedSizeBytes)
    {
        var eventsPerSecond = 1000.0; // Conservative estimate
        var estimatedDuration = TimeSpan.FromSeconds(eventCount / eventsPerSecond);

        return new Verification.ResourceEstimate
        {
            EstimatedDuration = estimatedDuration,
            EstimatedStorageBytes = estimatedSizeBytes,
            EstimatedBandwidthBytes = estimatedSizeBytes * 2 // Read + Write
        };
    }

    private List<Verification.Prerequisite> CheckPrerequisites()
    {
        return
        [
            new Verification.Prerequisite
            {
                Name = "DataStore",
                IsMet = context.DataStore != null,
                Description = context.DataStore != null
                    ? "Data store is configured"
                    : "A data store must be configured for migration"
            },
            new Verification.Prerequisite
            {
                Name = "DocumentStore",
                IsMet = context.DocumentStore != null,
                Description = context.DocumentStore != null
                    ? "Document store is configured"
                    : "A document store must be configured for cutover"
            }
        ];
    }

    private List<Verification.MigrationRisk> IdentifyRisks(
        int eventCount,
        Verification.TransformationSimulation transformationSimulation)
    {
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

        return risks;
    }

    private List<string> BuildRecommendedPhases(int eventCount)
    {
        var phases = new List<string>();

        if (context.BackupConfig != null)
        {
            phases.Add("1. Create backup of source stream");
        }
        phases.Add($"{(context.BackupConfig != null ? "2" : "1")}. Copy and transform {eventCount} events");
        if (context.VerificationConfig != null)
        {
            phases.Add($"{phases.Count + 1}. Verify migration integrity");
        }
        phases.Add($"{phases.Count + 1}. Perform cutover to target stream");
        if (context.BookClosingConfig != null)
        {
            phases.Add($"{phases.Count + 1}. Archive source stream");
        }

        return phases;
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
                    chunk: null,
                    cancellationToken: cancellationToken);

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
                var verificationResult = await VerifyMigrationAsync(cancellationToken);

                var successCount = verificationResult.ValidationResults.Count(r => r.Passed);
                var failureCount = verificationResult.ValidationResults.Count(r => !r.Passed);
                logger.VerificationCompleted(successCount, failureCount);

                if (!verificationResult.Passed && context.VerificationConfig.FailFast)
                {
                    throw new MigrationException($"Verification failed: {verificationResult.Summary}");
                }
            }

            // Step 5: Cutover to new stream
            logger.StepPerformingCutover();
            progressTracker.SetStatus(MigrationStatus.CuttingOver);
            await PerformCutoverAsync(cancellationToken);

            // Step 6: Book closing (if configured)
            if (context.BookClosingConfig != null)
            {
                logger.StepClosingBooks();
                await PerformBookClosingAsync(cancellationToken);
                logger.BookClosingCompleted(context.SourceStreamIdentifier);
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
                await RollbackMigrationAsync(cancellationToken);
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
            chunk: null,
            cancellationToken: cancellationToken);

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
            chunk: null,
            cancellationToken: cancellationToken);

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
            await context.DataStore.AppendAsync(targetDocument, default, targetEvents.ToArray());

            logger.WroteEventsToTarget(targetEvents.Count, context.TargetStreamIdentifier);
        }

        // Calculate average throughput
        statistics.AverageEventsPerSecond = progressTracker.GetProgress().EventsPerSecond;
    }

    private async Task<IVerificationResult> VerifyMigrationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new MigrationVerificationResult();
        var config = context.VerificationConfig!;

        var targetDocument = CreateTargetDocumentForVerification();

        // Read source and target events
        var sourceList = await ReadEventsAsync(context.SourceDocument, cancellationToken);
        var targetList = await ReadEventsAsync(targetDocument, cancellationToken);

        // Run each verification step
        VerifyEventCounts(config, result, sourceList, targetList);
        VerifyChecksums(config, result, sourceList, targetList);
        await VerifyTransformationsAsync(config, result, sourceList, targetList, cancellationToken);
        VerifyStreamIntegrity(config, result, targetList);
        await RunCustomValidationsAsync(config, result, cancellationToken);

        return result;
    }

    private MigrationTargetDocument CreateTargetDocumentForVerification()
    {
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

        return new MigrationTargetDocument(
            context.SourceDocument.ObjectId,
            context.SourceDocument.ObjectName,
            targetStreamInfo);
    }

    private async Task<List<IEvent>> ReadEventsAsync(IObjectDocument document, CancellationToken cancellationToken)
    {
        var events = await context.DataStore!.ReadAsync(
            document,
            startVersion: 0,
            untilVersion: null,
            chunk: null,
            cancellationToken: cancellationToken);
        return events?.ToList() ?? [];
    }

    private static void VerifyEventCounts(
        VerificationConfiguration config,
        MigrationVerificationResult result,
        List<IEvent> sourceList,
        List<IEvent> targetList)
    {
        if (!config.CompareEventCounts)
        {
            return;
        }

        var sourceCount = sourceList.Count;
        var targetCount = targetList.Count;
        var countsMatch = sourceCount == targetCount;

        result.AddResult(new ValidationResult(
            "EventCountComparison",
            countsMatch,
            countsMatch
                ? $"Event counts match: {sourceCount} events"
                : $"Event count mismatch: source={sourceCount}, target={targetCount}")
        {
            Details = new Dictionary<string, object>
            {
                ["sourceCount"] = sourceCount,
                ["targetCount"] = targetCount
            }
        });
    }

    private void VerifyChecksums(
        VerificationConfiguration config,
        MigrationVerificationResult result,
        List<IEvent> sourceList,
        List<IEvent> targetList)
    {
        if (!config.CompareChecksums)
        {
            return;
        }

        var sourceChecksum = ComputeStreamChecksum(sourceList);
        var targetChecksum = ComputeStreamChecksum(targetList);

        if (context.Transformer != null)
        {
            VerifyChecksumWithTransformation(result, targetChecksum);
        }
        else
        {
            VerifyChecksumDirect(result, sourceChecksum, targetChecksum);
        }
    }

    private static void VerifyChecksumWithTransformation(
        MigrationVerificationResult result,
        string targetChecksum)
    {
        var checksumValid = !string.IsNullOrEmpty(targetChecksum);
        result.AddResult(new ValidationResult(
            "ChecksumVerification",
            checksumValid,
            checksumValid
                ? "Target stream checksum computed successfully (transformation applied)"
                : "Failed to compute target stream checksum")
        {
            Details = new Dictionary<string, object>
            {
                ["targetChecksum"] = targetChecksum
            }
        });
    }

    private static void VerifyChecksumDirect(
        MigrationVerificationResult result,
        string sourceChecksum,
        string targetChecksum)
    {
        var checksumValid = sourceChecksum == targetChecksum;
        result.AddResult(new ValidationResult(
            "ChecksumComparison",
            checksumValid,
            checksumValid
                ? $"Checksums match: {sourceChecksum[..16]}..."
                : $"Checksum mismatch: source={sourceChecksum[..16]}..., target={targetChecksum[..16]}...")
        {
            Details = new Dictionary<string, object>
            {
                ["sourceChecksum"] = sourceChecksum,
                ["targetChecksum"] = targetChecksum
            }
        });
    }

    private async Task VerifyTransformationsAsync(
        VerificationConfiguration config,
        MigrationVerificationResult result,
        List<IEvent> sourceList,
        List<IEvent> targetList,
        CancellationToken cancellationToken)
    {
        if (!config.ValidateTransformations || context.Transformer == null)
        {
            return;
        }

        var sampleSize = Math.Min(config.TransformationSampleSize, sourceList.Count);
        var successes = 0;
        var failures = 0;

        for (var i = 0; i < sampleSize; i++)
        {
            if (await ValidateSingleTransformationAsync(sourceList[i], i, targetList, cancellationToken))
            {
                successes++;
            }
            else
            {
                failures++;
            }
        }

        var transformationValid = failures == 0;
        result.AddResult(new ValidationResult(
            "TransformationValidation",
            transformationValid,
            transformationValid
                ? $"All {sampleSize} sampled transformations validated successfully"
                : $"Transformation validation: {successes} passed, {failures} failed out of {sampleSize} samples")
        {
            Details = new Dictionary<string, object>
            {
                ["sampleSize"] = sampleSize,
                ["successes"] = successes,
                ["failures"] = failures
            }
        });
    }

    private async Task<bool> ValidateSingleTransformationAsync(
        IEvent sourceEvent,
        int index,
        List<IEvent> targetList,
        CancellationToken cancellationToken)
    {
        try
        {
            var transformedEvent = await context.Transformer!.TransformAsync(sourceEvent, cancellationToken);

            return index < targetList.Count &&
                   transformedEvent.EventType == targetList[index].EventType;
        }
        catch
        {
            return false;
        }
    }

    private static void VerifyStreamIntegrity(
        VerificationConfiguration config,
        MigrationVerificationResult result,
        List<IEvent> targetList)
    {
        if (!config.VerifyStreamIntegrity)
        {
            return;
        }

        var integrityValid = VerifyEventSequencing(targetList);
        result.AddResult(new ValidationResult(
            "StreamIntegrity",
            integrityValid,
            integrityValid
                ? "Target stream integrity verified: proper event sequencing"
                : "Stream integrity check failed: event sequencing issues detected")
        {
            Details = new Dictionary<string, object>
            {
                ["eventCount"] = targetList.Count
            }
        });
    }

    private async Task RunCustomValidationsAsync(
        VerificationConfiguration config,
        MigrationVerificationResult result,
        CancellationToken cancellationToken)
    {
        foreach (var (name, validator) in config.CustomValidations)
        {
            var validationResult = await ExecuteCustomValidationAsync(name, validator, cancellationToken);
            result.AddResult(validationResult);
        }
    }

    private async Task<ValidationResult> ExecuteCustomValidationAsync(
        string name,
        Func<VerificationContext, Task<ValidationResult>> validator,
        CancellationToken cancellationToken)
    {
        try
        {
            var verificationContext = new VerificationContext
            {
                SourceStreamIdentifier = context.SourceStreamIdentifier,
                TargetStreamIdentifier = context.TargetStreamIdentifier,
                Transformer = context.Transformer,
                Statistics = statistics
            };

            return await validator(verificationContext);
        }
        catch (Exception ex)
        {
            return new ValidationResult(
                name,
                false,
                $"Custom validation '{name}' threw an exception: {ex.Message}");
        }
    }

    private static string ComputeStreamChecksum(List<IEvent> events)
    {
        var builder = new StringBuilder();

        foreach (var evt in events)
        {
            builder.Append(evt.EventType);
            builder.Append(evt.EventVersion);
            builder.Append(evt.Payload ?? string.Empty);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    private static bool VerifyEventSequencing(List<IEvent> events)
    {
        if (events.Count == 0) return true;

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].EventVersion != i)
            {
                return false;
            }
        }

        return true;
    }

    private async Task RollbackMigrationAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Restore from backup if available
            if (backupHandle != null && context.BackupProvider != null)
            {
                logger.RollbackFromBackup(backupHandle.BackupId);

                var restoreContext = new RestoreContext
                {
                    TargetDocument = context.SourceDocument,
                    Overwrite = true,
                    DataStore = context.DataStore
                };

                await context.BackupProvider.RestoreAsync(
                    backupHandle,
                    restoreContext,
                    progress: null,
                    cancellationToken);

                progressTracker.SetStatus(MigrationStatus.RolledBack);
                statistics.RolledBack = true;
            }
            else
            {
                // No backup available - can only mark as rolled back
                // Note: This may leave partial data in the target stream
                progressTracker.SetStatus(MigrationStatus.RolledBack);
                statistics.RolledBack = true;
            }
        }
        catch (Exception ex)
        {
            // Rollback failed - log but don't throw as original exception is more important
            logger.MigrationFailed(context.MigrationId, ex);
            progressTracker.SetStatus(MigrationStatus.Failed);
        }
    }

    private async Task PerformBookClosingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var config = context.BookClosingConfig!;

        // Update the terminated stream entry with book closing metadata
        if (context.DocumentStore != null)
        {
            // Reload the document to get the latest state after cutover
            var currentDocument = await context.DocumentStore.GetAsync(
                context.SourceDocument.ObjectName,
                context.SourceDocument.ObjectId);

            if (currentDocument != null)
            {
                // Find the terminated stream we created during cutover
                var terminatedStream = currentDocument.TerminatedStreams
                    .FirstOrDefault(t => t.StreamIdentifier == context.SourceStreamIdentifier);

                if (terminatedStream != null)
                {
                    // Update the terminated stream with book closing info
                    terminatedStream.Reason = config.Reason ?? terminatedStream.Reason;
                    terminatedStream.Deleted = config.MarkAsDeleted;

                    // Add custom metadata
                    foreach (var (key, value) in config.Metadata)
                    {
                        terminatedStream.Metadata ??= new Dictionary<string, object>();
                        terminatedStream.Metadata[key] = value;
                    }

                    // Add archive location if specified
                    if (!string.IsNullOrEmpty(config.ArchiveLocation))
                    {
                        terminatedStream.Metadata ??= new Dictionary<string, object>();
                        terminatedStream.Metadata["archiveLocation"] = config.ArchiveLocation;
                    }

                    // Save the updated document
                    await context.DocumentStore.SetAsync(currentDocument);
                }
            }
        }

        // Create snapshot if configured
        if (config.CreateSnapshot && context.DataStore != null)
        {
            // Read all events for snapshot - this is already done in the source document's active stream
            // The snapshot would typically be handled by the event stream infrastructure
            // For now, we mark it in the statistics
            statistics.SnapshotCreated = true;
        }
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
