namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using ErikLieben.FA.ES.EventStreamManagement.BookClosing;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;
using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using ErikLieben.FA.ES.EventStreamManagement.Verification;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// Builder for configuring and executing bulk migration operations on multiple documents.
/// </summary>
public class BulkMigrationBuilder : IMigrationBuilder
{
    private readonly List<IObjectDocument> documents;
    private readonly IDataStore dataStore;
    private readonly IDocumentStore documentStore;
    private readonly IDistributedLockProvider lockProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<BulkMigrationBuilder> logger;

    private IEventTransformer? transformer;
    private ITransformationPipeline? pipeline;
    private DistributedLockOptions? lockOptions;
    private Action<IBackupBuilder>? backupConfigure;
    private Action<IBookClosingBuilder>? bookClosingConfigure;
    private Action<IVerificationBuilder>? verificationConfigure;
    private Action<IProgressBuilder>? progressConfigure;
    private bool isDryRun;
    private bool supportsPause;
    private bool supportsRollback;
    private Func<IObjectDocument, string>? streamIdentifierFactory;
    private int maxConcurrency = 4;
    private bool continueOnError = true;
    private Action<BulkMigrationProgress>? onProgress;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkMigrationBuilder"/> class.
    /// </summary>
    public BulkMigrationBuilder(
        IEnumerable<IObjectDocument> documents,
        IDataStore dataStore,
        IDocumentStore documentStore,
        IDistributedLockProvider lockProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(dataStore);
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(lockProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.documents = documents.ToList();
        if (this.documents.Count == 0)
        {
            throw new ArgumentException("At least one document is required", nameof(documents));
        }

        this.dataStore = dataStore;
        this.documentStore = documentStore;
        this.lockProvider = lockProvider;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<BulkMigrationBuilder>();
    }

    /// <summary>
    /// Sets the maximum number of concurrent migrations.
    /// </summary>
    public BulkMigrationBuilder WithMaxConcurrency(int maxConcurrency)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Must be at least 1");
        }

        this.maxConcurrency = maxConcurrency;
        return this;
    }

    /// <summary>
    /// Sets whether to continue processing when a migration fails.
    /// </summary>
    public BulkMigrationBuilder WithContinueOnError(bool continueOnError = true)
    {
        this.continueOnError = continueOnError;
        return this;
    }

    /// <summary>
    /// Sets the progress callback for bulk operations.
    /// </summary>
    public BulkMigrationBuilder WithBulkProgress(Action<BulkMigrationProgress> onProgress)
    {
        this.onProgress = onProgress;
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder CopyToNewStream(string streamIdentifier)
    {
        ArgumentNullException.ThrowIfNull(streamIdentifier);
        // For bulk migrations, we use a factory pattern to generate unique stream identifiers
        this.streamIdentifierFactory = _ => streamIdentifier;
        return this;
    }

    /// <summary>
    /// Sets a factory function to generate target stream identifiers for each document.
    /// </summary>
    public BulkMigrationBuilder CopyToNewStreams(Func<IObjectDocument, string> identifierFactory)
    {
        this.streamIdentifierFactory = identifierFactory ?? throw new ArgumentNullException(nameof(identifierFactory));
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithTransformation(IEventTransformer transformer)
    {
        this.transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithPipeline(Action<ITransformationPipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new TransformationPipelineBuilder(loggerFactory);
        configure(builder);
        this.pipeline = builder.Build();
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithDistributedLock(Action<IDistributedLockOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new DistributedLockOptions();
        configure(options);
        this.lockOptions = options;
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithBackup(Action<IBackupBuilder> configure)
    {
        this.backupConfigure = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithBookClosing(Action<IBookClosingBuilder> configure)
    {
        this.bookClosingConfigure = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithVerification(Action<IVerificationBuilder> configure)
    {
        this.verificationConfigure = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder DryRun()
    {
        this.isDryRun = true;
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithProgress(Action<IProgressBuilder> configure)
    {
        this.progressConfigure = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithPauseSupport()
    {
        this.supportsPause = true;
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithRollbackSupport()
    {
        this.supportsRollback = true;
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder FromDryRunPlan(IMigrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        this.isDryRun = false;
        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithLiveMigration(Action<ILiveMigrationOptions>? configure = null)
    {
        // Live migration for bulk operations is not supported
        throw new NotSupportedException(
            "Live migration is not supported for bulk operations. Use ForDocument() for individual live migrations.");
    }

    /// <inheritdoc/>
    public Task<LiveMigrationResult> ExecuteLiveMigrationAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Live migration is not supported for bulk operations. Use ForDocument() for individual live migrations.");
    }

    /// <inheritdoc/>
    public async Task<IMigrationResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        var stopwatch = Stopwatch.StartNew();
        logger.StartingBulkMigration(documents.Count, maxConcurrency);

        var successfulMigrations = new ConcurrentBag<IMigrationResult>();
        var failedMigrations = new ConcurrentBag<(IObjectDocument Document, Exception Exception)>();
        var processedCount = 0;

        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = documents.Select(async document =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await ExecuteSingleMigrationAsync(document, cancellationToken);
                successfulMigrations.Add(result);
            }
            catch (Exception ex) when (continueOnError)
            {
                failedMigrations.Add((document, ex));
                logger.MigrationFailed(document.ObjectId, ex);
            }
            finally
            {
                semaphore.Release();
                var current = Interlocked.Increment(ref processedCount);
                onProgress?.Invoke(new BulkMigrationProgress
                {
                    TotalDocuments = documents.Count,
                    ProcessedDocuments = current,
                    SuccessfulMigrations = successfulMigrations.Count,
                    FailedMigrations = failedMigrations.Count,
                    CurrentDocumentId = document.ObjectId
                });
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        logger.BulkMigrationCompleted(
            successfulMigrations.Count,
            failedMigrations.Count,
            stopwatch.Elapsed);

        return new BulkMigrationResult
        {
            MigrationId = Guid.NewGuid(),
            Success = failedMigrations.IsEmpty,
            TotalDocuments = documents.Count,
            SuccessfulCount = successfulMigrations.Count,
            FailedCount = failedMigrations.Count,
            IndividualResults = successfulMigrations.ToList(),
            Failures = failedMigrations.Select(f => new MigrationFailure
            {
                ObjectId = f.Document.ObjectId,
                ObjectName = f.Document.ObjectName,
                ErrorMessage = f.Exception.Message,
                Exception = f.Exception
            }).ToList(),
            Duration = stopwatch.Elapsed
        };
    }

    private async Task<IMigrationResult> ExecuteSingleMigrationAsync(
        IObjectDocument document,
        CancellationToken cancellationToken)
    {
        var targetStreamId = streamIdentifierFactory?.Invoke(document)
            ?? $"{document.Active.StreamIdentifier}-migrated-{Guid.NewGuid():N}";

        var builder = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

        builder.CopyToNewStream(targetStreamId);

        ApplyTransformationOptions(builder);
        ApplyLockOptions(builder);
        ApplyOptionalBuilderActions(builder);
        ApplyMigrationFlags(builder);

        return await builder.ExecuteAsync(cancellationToken);
    }

    private void ApplyTransformationOptions(MigrationBuilder builder)
    {
        if (transformer != null)
        {
            builder.WithTransformation(transformer);
        }

        if (pipeline != null)
        {
            builder.WithPipeline(_ => { }); // Pipeline already built
        }
    }

    private void ApplyLockOptions(MigrationBuilder builder)
    {
        if (lockOptions == null)
        {
            return;
        }

        builder.WithDistributedLock(o =>
        {
            o.LockTimeout(lockOptions.LockTimeoutValue);
            o.HeartbeatInterval(lockOptions.HeartbeatIntervalValue);
            if (lockOptions.LockLocation != null)
            {
                o.UseLease(lockOptions.LockLocation);
            }
            if (lockOptions.ProviderName != null)
            {
                o.UseProvider(lockOptions.ProviderName);
            }
        });
    }

    private void ApplyOptionalBuilderActions(MigrationBuilder builder)
    {
        if (backupConfigure != null)
        {
            builder.WithBackup(backupConfigure);
        }

        if (bookClosingConfigure != null)
        {
            builder.WithBookClosing(bookClosingConfigure);
        }

        if (verificationConfigure != null)
        {
            builder.WithVerification(verificationConfigure);
        }

        if (progressConfigure != null)
        {
            builder.WithProgress(progressConfigure);
        }
    }

    private void ApplyMigrationFlags(MigrationBuilder builder)
    {
        if (isDryRun)
        {
            builder.DryRun();
        }

        if (supportsPause)
        {
            builder.WithPauseSupport();
        }

        if (supportsRollback)
        {
            builder.WithRollbackSupport();
        }
    }

    private void ValidateConfiguration()
    {
        if (streamIdentifierFactory == null)
        {
            // Use a default factory that generates unique identifiers
            streamIdentifierFactory = doc =>
                $"{doc.Active.StreamIdentifier}-migrated-{Guid.NewGuid():N}";
        }
    }
}

/// <summary>
/// Progress information for bulk migration operations.
/// </summary>
public class BulkMigrationProgress
{
    /// <summary>
    /// Gets or sets the total number of documents to migrate.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Gets or sets the number of documents processed.
    /// </summary>
    public int ProcessedDocuments { get; set; }

    /// <summary>
    /// Gets or sets the number of successful migrations.
    /// </summary>
    public int SuccessfulMigrations { get; set; }

    /// <summary>
    /// Gets or sets the number of failed migrations.
    /// </summary>
    public int FailedMigrations { get; set; }

    /// <summary>
    /// Gets or sets the current document being processed.
    /// </summary>
    public string? CurrentDocumentId { get; set; }

    /// <summary>
    /// Gets the percentage complete.
    /// </summary>
    public double PercentageComplete => TotalDocuments > 0
        ? (double)ProcessedDocuments / TotalDocuments * 100.0
        : 0.0;
}

/// <summary>
/// Result of a bulk migration operation.
/// </summary>
public class BulkMigrationResult : IMigrationResult
{
    /// <inheritdoc/>
    public Guid MigrationId { get; set; }

    /// <inheritdoc/>
    public bool Success { get; set; }

    /// <inheritdoc/>
    public MigrationStatus Status => Success ? MigrationStatus.Completed : MigrationStatus.Failed;

    /// <inheritdoc/>
    public string? ErrorMessage => Failures.Count > 0
        ? $"{Failures.Count} migration(s) failed: {string.Join("; ", Failures.Take(3).Select(f => f.ErrorMessage))}"
        : null;

    /// <inheritdoc/>
    public Exception? Exception => Failures.Count > 0 ? Failures[0].Exception : null;

    /// <inheritdoc/>
    public IMigrationProgress Progress => new BulkMigrationProgressSnapshot
    {
        Status = Status,
        PercentageComplete = 100.0,
        EventsProcessed = IndividualResults.Sum(r => r.Progress.EventsProcessed),
        TotalEvents = IndividualResults.Sum(r => r.Progress.TotalEvents)
    };

    /// <inheritdoc/>
    public IVerificationResult? VerificationResult => null;

    /// <inheritdoc/>
    public IMigrationPlan? Plan => null;

    /// <inheritdoc/>
    public TimeSpan Duration { get; set; }

    /// <inheritdoc/>
    public MigrationStatistics Statistics => new()
    {
        TotalEvents = IndividualResults.Sum(r => r.Statistics.TotalEvents),
        EventsTransformed = IndividualResults.Sum(r => r.Statistics.EventsTransformed),
        TransformationFailures = IndividualResults.Sum(r => r.Statistics.TransformationFailures),
        AverageEventsPerSecond = Duration.TotalSeconds > 0
            ? IndividualResults.Sum(r => r.Statistics.TotalEvents) / Duration.TotalSeconds
            : 0,
        TotalBytes = IndividualResults.Sum(r => r.Statistics.TotalBytes),
        StartedAt = IndividualResults.Min(r => r.Statistics.StartedAt),
        CompletedAt = IndividualResults.Max(r => r.Statistics.CompletedAt),
        RolledBack = IndividualResults.Any(r => r.Statistics.RolledBack)
    };

    /// <summary>
    /// Gets or sets the total number of documents.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Gets or sets the number of successful migrations.
    /// </summary>
    public int SuccessfulCount { get; set; }

    /// <summary>
    /// Gets or sets the number of failed migrations.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the individual migration results.
    /// </summary>
    public IReadOnlyList<IMigrationResult> IndividualResults { get; set; } = [];

    /// <summary>
    /// Gets or sets information about failed migrations.
    /// </summary>
    public IReadOnlyList<MigrationFailure> Failures { get; set; } = [];
}

/// <summary>
/// Progress snapshot for bulk migrations.
/// </summary>
internal class BulkMigrationProgressSnapshot : IMigrationProgress
{
    public Guid MigrationId { get; set; }
    public MigrationStatus Status { get; set; }
    public MigrationPhase CurrentPhase => MigrationPhase.BookClosed;
    public double PercentageComplete { get; set; }
    public long EventsProcessed { get; set; }
    public long TotalEvents { get; set; }
    public double EventsPerSecond => Elapsed.TotalSeconds > 0 ? EventsProcessed / Elapsed.TotalSeconds : 0;
    public TimeSpan Elapsed { get; set; }
    public TimeSpan? EstimatedRemaining => null;
    public bool IsPaused => false;
    public bool CanPause => false;
    public bool CanRollback => false;
    public IReadOnlyDictionary<string, object> CustomMetrics => new Dictionary<string, object>();
    public string? ErrorMessage => null;
}

/// <summary>
/// Information about a failed migration.
/// </summary>
public class MigrationFailure
{
    /// <summary>
    /// Gets or sets the object ID that failed.
    /// </summary>
    public required string ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the object name.
    /// </summary>
    public required string ObjectName { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public required string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception.
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Logging extensions for BulkMigrationBuilder.
/// </summary>
internal static partial class BulkMigrationBuilderLoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Starting bulk migration of {DocumentCount} documents with concurrency {MaxConcurrency}")]
    public static partial void StartingBulkMigration(this ILogger logger, int documentCount, int maxConcurrency);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migration failed for document {ObjectId}")]
    public static partial void MigrationFailed(this ILogger logger, string objectId, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bulk migration completed: {SuccessCount} succeeded, {FailedCount} failed in {ElapsedTime}")]
    public static partial void BulkMigrationCompleted(this ILogger logger, int successCount, int failedCount, TimeSpan elapsedTime);
}
