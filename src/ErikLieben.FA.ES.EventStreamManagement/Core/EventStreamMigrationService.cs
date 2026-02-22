namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

/// <summary>
/// Main service for managing event stream migrations.
/// </summary>
public class EventStreamMigrationService : IEventStreamMigrationService
{
    private readonly IDataStore dataStore;
    private readonly IDocumentStore documentStore;
    private readonly IDistributedLockProvider lockProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<EventStreamMigrationService> logger;
    private readonly ConcurrentDictionary<Guid, MigrationProgressTracker> activeMigrations;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamMigrationService"/> class.
    /// </summary>
    public EventStreamMigrationService(
        IDataStore dataStore,
        IDocumentStore documentStore,
        IDistributedLockProvider lockProvider,
        ILoggerFactory loggerFactory)
    {
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        this.documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        this.lockProvider = lockProvider ?? throw new ArgumentNullException(nameof(lockProvider));
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        this.logger = loggerFactory.CreateLogger<EventStreamMigrationService>();
        this.activeMigrations = new ConcurrentDictionary<Guid, MigrationProgressTracker>();
    }

    /// <inheritdoc/>
    public IMigrationBuilder ForDocument(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        logger.CreatingMigrationBuilder(document.ObjectId);

        return new MigrationBuilder(
            document,
            dataStore,
            documentStore,
            lockProvider,
            loggerFactory);
    }

    /// <inheritdoc/>
    public IMigrationBuilder ForDocuments(IEnumerable<IObjectDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var documentList = documents.ToList();
        if (documentList.Count == 0)
        {
            throw new ArgumentException("At least one document is required", nameof(documents));
        }

        logger.CreatingBulkMigrationBuilder(documentList.Count);

        return new BulkMigrationBuilder(
            documentList,
            dataStore,
            documentStore,
            lockProvider,
            loggerFactory);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<IMigrationProgress>> GetActiveMigrationsAsync(
        CancellationToken cancellationToken = default)
    {
        var progress = activeMigrations.Values
            .Select(tracker => tracker.GetProgress())
            .ToList();

        logger.RetrievedActiveMigrations(progress.Count);

        return Task.FromResult<IEnumerable<IMigrationProgress>>(progress);
    }

    /// <inheritdoc/>
    public Task<IMigrationProgress?> GetMigrationStatusAsync(
        Guid migrationId,
        CancellationToken cancellationToken = default)
    {
        if (activeMigrations.TryGetValue(migrationId, out var tracker))
        {
            return Task.FromResult<IMigrationProgress?>(tracker.GetProgress());
        }

        logger.MigrationNotFound(migrationId);

        return Task.FromResult<IMigrationProgress?>(null);
    }

    /// <inheritdoc/>
    public Task PauseMigrationAsync(
        Guid migrationId,
        CancellationToken cancellationToken = default)
    {
        if (activeMigrations.TryGetValue(migrationId, out var tracker))
        {
            tracker.SetPaused(true);

            logger.PausedMigration(migrationId);
        }
        else
        {
            logger.CannotPauseMigration(migrationId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResumeMigrationAsync(
        Guid migrationId,
        CancellationToken cancellationToken = default)
    {
        if (activeMigrations.TryGetValue(migrationId, out var tracker))
        {
            tracker.SetPaused(false);

            logger.ResumedMigration(migrationId);
        }
        else
        {
            logger.CannotResumeMigration(migrationId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CancelMigrationAsync(
        Guid migrationId,
        CancellationToken cancellationToken = default)
    {
        if (activeMigrations.TryRemove(migrationId, out var tracker))
        {
            tracker.SetStatus(MigrationStatus.Cancelled);

            logger.CancelledMigration(migrationId);
        }
        else
        {
            logger.CannotCancelMigration(migrationId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers a migration tracker for monitoring.
    /// </summary>
    internal void RegisterMigration(Guid migrationId, MigrationProgressTracker tracker)
    {
        activeMigrations[migrationId] = tracker;
    }

    /// <summary>
    /// Unregisters a migration tracker when complete.
    /// </summary>
    internal void UnregisterMigration(Guid migrationId)
    {
        activeMigrations.TryRemove(migrationId, out _);
    }
}
