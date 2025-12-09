#pragma warning disable CS0618 // Type or member is obsolete - supporting legacy connection name properties during migration

namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using ErikLieben.FA.ES.EventStreamManagement.BookClosing;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using ErikLieben.FA.ES.EventStreamManagement.Verification;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fluent builder for configuring migration operations.
/// </summary>
public class MigrationBuilder : IMigrationBuilder
{
    private readonly MigrationContext context;
    private readonly IDataStore dataStore;
    private readonly IDocumentStore documentStore;
    private readonly IDistributedLockProvider lockProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<MigrationBuilder> logger;

    private IEventTransformer? transformer;
    private ITransformationPipeline? pipeline;
    private DistributedLockOptions? lockOptions;
    private BackupConfiguration? backupConfig;
    private BookClosingConfiguration? bookClosingConfig;
    private VerificationConfiguration? verificationConfig;
    private ProgressConfiguration? progressConfig;
    private bool isDryRun;
    private bool supportsPause;
    private bool supportsRollback;
    private LiveMigrationOptions? liveMigrationOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationBuilder"/> class.
    /// </summary>
    public MigrationBuilder(
        IObjectDocument document,
        IDataStore dataStore,
        IDocumentStore documentStore,
        IDistributedLockProvider lockProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(dataStore);
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(lockProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        this.dataStore = dataStore;
        this.documentStore = documentStore;
        this.lockProvider = lockProvider;
        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<MigrationBuilder>();

        this.context = new MigrationContext
        {
            SourceDocument = document,
            SourceStreamIdentifier = document.Active.StreamIdentifier,
            TargetStreamIdentifier = string.Empty // Will be set by CopyToNewStream
        };
    }

    /// <inheritdoc/>
    public IMigrationBuilder CopyToNewStream(string streamIdentifier)
    {
        ArgumentNullException.ThrowIfNull(streamIdentifier);
        context.TargetStreamIdentifier = streamIdentifier;
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
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new BackupBuilder();
        configure(builder);
        this.backupConfig = builder.Build();

        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithBookClosing(Action<IBookClosingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new BookClosingBuilder();
        configure(builder);
        this.bookClosingConfig = builder.Build();

        return this;
    }

    /// <inheritdoc/>
    public IMigrationBuilder WithVerification(Action<IVerificationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new VerificationBuilder();
        configure(builder);
        this.verificationConfig = builder.Build();

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
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ProgressBuilder();
        configure(builder);
        this.progressConfig = builder.Build();

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
        this.liveMigrationOptions = new LiveMigrationOptions();
        configure?.Invoke(this.liveMigrationOptions);
        return this;
    }

    /// <inheritdoc/>
    public async Task<LiveMigrationResult> ExecuteLiveMigrationAsync(CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        if (liveMigrationOptions == null)
        {
            throw new InvalidOperationException(
                "Live migration not configured. Call WithLiveMigration() before ExecuteLiveMigrationAsync().");
        }

        logger.StartingLiveMigration(context.MigrationId, context.SourceStreamIdentifier, context.TargetStreamIdentifier);

        // Create target stream info based on source
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

        var liveMigrationContext = new LiveMigrationContext
        {
            MigrationId = context.MigrationId,
            SourceDocument = context.SourceDocument,
            SourceStreamId = context.SourceStreamIdentifier,
            TargetStreamId = context.TargetStreamIdentifier,
            TargetDocument = targetDocument,
            DataStore = dataStore,
            DocumentStore = documentStore,
            Options = liveMigrationOptions,
            Transformer = transformer ?? pipeline
        };

        var executor = new LiveMigrationExecutor(liveMigrationContext, loggerFactory);
        return await executor.ExecuteAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IMigrationResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Validate configuration
        ValidateConfiguration();

        // Build final context
        context.Transformer = transformer ?? pipeline;
        context.Pipeline = pipeline;
        context.LockOptions = lockOptions;
        context.BackupConfig = backupConfig;
        context.BookClosingConfig = bookClosingConfig;
        context.VerificationConfig = verificationConfig;
        context.ProgressConfig = progressConfig;
        context.IsDryRun = isDryRun;
        context.SupportsPause = supportsPause;
        context.SupportsRollback = supportsRollback;
        context.DataStore = dataStore;
        context.DocumentStore = documentStore;

        logger.StartingMigration(context.MigrationId, context.SourceStreamIdentifier, context.TargetStreamIdentifier, isDryRun);

        // Create executor and run migration
        var executor = new MigrationExecutor(context, lockProvider, loggerFactory);
        return await executor.ExecuteAsync(cancellationToken);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(context.TargetStreamIdentifier))
        {
            throw new InvalidOperationException(
                "Target stream identifier must be specified using CopyToNewStream()");
        }

        if (context.SourceStreamIdentifier == context.TargetStreamIdentifier)
        {
            throw new InvalidOperationException(
                "Source and target stream identifiers must be different");
        }
    }
}
