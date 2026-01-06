namespace ErikLieben.FA.ES.EventStreamManagement.Backup;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// Implementation of <see cref="IBackupRestoreService"/> that provides general-purpose
/// backup and restore capabilities for event streams.
/// </summary>
public class BackupRestoreService : IBackupRestoreService
{
    private readonly IBackupProvider backupProvider;
    private readonly IDocumentStore documentStore;
    private readonly IDataStore dataStore;
    private readonly IBackupRegistry? backupRegistry;
    private readonly ILogger<BackupRestoreService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupRestoreService"/> class.
    /// </summary>
    /// <param name="backupProvider">The backup provider for storage operations.</param>
    /// <param name="documentStore">The document store for retrieving object documents.</param>
    /// <param name="dataStore">The data store for reading events.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="backupRegistry">Optional backup registry for listing and tracking backups.</param>
    public BackupRestoreService(
        IBackupProvider backupProvider,
        IDocumentStore documentStore,
        IDataStore dataStore,
        ILogger<BackupRestoreService> logger,
        IBackupRegistry? backupRegistry = null)
    {
        this.backupProvider = backupProvider ?? throw new ArgumentNullException(nameof(backupProvider));
        this.documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.backupRegistry = backupRegistry;
    }

    #region Single Stream Operations

    /// <inheritdoc/>
    public async Task<IBackupHandle> BackupStreamAsync(
        string objectName,
        string objectId,
        BackupOptions? options = null,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        logger.BackupStreamStarting(objectName, objectId);

        // Get the document
        var document = await documentStore.GetAsync(objectName, objectId);

        return await BackupDocumentAsync(document, options, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IBackupHandle> BackupDocumentAsync(
        IObjectDocument document,
        BackupOptions? options = null,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= BackupOptions.Default;

        logger.BackupDocumentStarting(document.ObjectName, document.ObjectId);

        // Read all events from the stream
        var events = await dataStore.ReadAsync(document);
        var eventList = events?.ToList() ?? [];

        // Report initial progress
        progress?.Report(new BackupProgress
        {
            EventsBackedUp = 0,
            TotalEvents = eventList.Count,
            BytesWritten = 0
        });

        // Create backup context
        var context = new BackupContext
        {
            Document = document,
            Configuration = options.ToConfiguration(),
            Events = eventList
        };

        // Perform backup
        var handle = await backupProvider.BackupAsync(context, progress, cancellationToken);

        // Register the backup if registry is available
        if (backupRegistry != null)
        {
            await backupRegistry.RegisterBackupAsync(handle, options, cancellationToken);
        }

        logger.BackupCompleted(handle.BackupId, document.ObjectId, handle.EventCount);

        return handle;
    }

    /// <inheritdoc/>
    public async Task RestoreStreamAsync(
        IBackupHandle handle,
        RestoreOptions? options = null,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        options ??= RestoreOptions.Default;

        logger.RestoreStarting(handle.BackupId, handle.ObjectId);

        // Validate if requested
        if (options.ValidateBeforeRestore)
        {
            var isValid = await backupProvider.ValidateBackupAsync(handle, cancellationToken);
            if (!isValid)
            {
                throw new InvalidOperationException($"Backup {handle.BackupId} failed validation and cannot be restored.");
            }
        }

        // Get or create the target document (suppress nullable warning - CreateAsync uses [MaybeNull])
#pragma warning disable CS8602
        var targetDocument = await documentStore.CreateAsync(handle.ObjectName, handle.ObjectId)
            ?? throw new InvalidOperationException($"Failed to create target document for {handle.ObjectName}/{handle.ObjectId}");
#pragma warning restore CS8602

        // Check if stream already has data and we're not overwriting
        if (!options.Overwrite && targetDocument.Active.CurrentStreamVersion >= 0)
        {
            throw new InvalidOperationException(
                $"Stream {handle.ObjectId} already exists with version {targetDocument.Active.CurrentStreamVersion}. " +
                "Set Overwrite to true to restore over existing data.");
        }

        // Create restore context
        var context = options.ToContext(targetDocument, dataStore);

        // Perform restore
        await backupProvider.RestoreAsync(handle, context, progress, cancellationToken);

        logger.RestoreCompleted(handle.BackupId, handle.ObjectId);
    }

    /// <inheritdoc/>
    public async Task RestoreToNewStreamAsync(
        IBackupHandle handle,
        string targetObjectId,
        RestoreOptions? options = null,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetObjectId);

        options ??= RestoreOptions.Default;

        logger.RestoreToNewStreamStarting(handle.BackupId, handle.ObjectId, targetObjectId);

        // Validate if requested
        if (options.ValidateBeforeRestore)
        {
            var isValid = await backupProvider.ValidateBackupAsync(handle, cancellationToken);
            if (!isValid)
            {
                throw new InvalidOperationException($"Backup {handle.BackupId} failed validation and cannot be restored.");
            }
        }

        // Create the new target document (suppress nullable warning - CreateAsync uses [MaybeNull])
#pragma warning disable CS8602
        var targetDocument = await documentStore.CreateAsync(handle.ObjectName, targetObjectId)
            ?? throw new InvalidOperationException($"Failed to create target document for {handle.ObjectName}/{targetObjectId}");
#pragma warning restore CS8602

        // Check if stream already exists
        if (!options.Overwrite && targetDocument.Active.CurrentStreamVersion >= 0)
        {
            throw new InvalidOperationException(
                $"Stream {targetObjectId} already exists. Set Overwrite to true to restore over existing data.");
        }

        // Create restore context for the new target
        var context = options.ToContext(targetDocument, dataStore);

        // Perform restore
        await backupProvider.RestoreAsync(handle, context, progress, cancellationToken);

        logger.RestoreToNewStreamCompleted(handle.BackupId, targetObjectId);
    }

    #endregion

    #region Bulk Operations

    /// <inheritdoc/>
    public async Task<BulkBackupResult> BackupManyAsync(
        IEnumerable<string> objectIds,
        string objectName,
        BulkBackupOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        options ??= BulkBackupOptions.Default;

        var idList = objectIds.ToList();
        var stopwatch = Stopwatch.StartNew();

        logger.BulkBackupStarting(objectName, idList.Count, options.MaxConcurrency);

        var successfulBackups = new ConcurrentBag<IBackupHandle>();
        var failedBackups = new ConcurrentBag<BackupFailure>();
        var processedCount = 0;

        // Use semaphore for concurrency control
        using var semaphore = new SemaphoreSlim(options.MaxConcurrency);

        var tasks = idList.Select(async objectId =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var handle = await BackupStreamAsync(objectName, objectId, options, null, cancellationToken);
                successfulBackups.Add(handle);
            }
            catch (Exception ex) when (options.ContinueOnError)
            {
                failedBackups.Add(new BackupFailure
                {
                    ObjectId = objectId,
                    ObjectName = objectName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
            }
            finally
            {
                semaphore.Release();
                var current = Interlocked.Increment(ref processedCount);
                options.OnProgress?.Invoke(new BulkBackupProgress
                {
                    TotalStreams = idList.Count,
                    ProcessedStreams = current,
                    SuccessfulBackups = successfulBackups.Count,
                    FailedBackups = failedBackups.Count,
                    CurrentStreamId = objectId
                });
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var result = new BulkBackupResult
        {
            TotalProcessed = idList.Count,
            SuccessCount = successfulBackups.Count,
            FailureCount = failedBackups.Count,
            SuccessfulBackups = successfulBackups.ToList(),
            FailedBackups = failedBackups.ToList(),
            ElapsedTime = stopwatch.Elapsed
        };

        logger.BulkBackupCompleted(result.SuccessCount, result.FailureCount, stopwatch.Elapsed);

        return result;
    }

    /// <inheritdoc/>
    public async Task<BulkRestoreResult> RestoreManyAsync(
        IEnumerable<IBackupHandle> handles,
        BulkRestoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handles);

        options ??= BulkRestoreOptions.Default;

        var handleList = handles.ToList();
        var stopwatch = Stopwatch.StartNew();

        logger.BulkRestoreStarting(handleList.Count, options.MaxConcurrency);

        var successfulRestores = new ConcurrentBag<Guid>();
        var failedRestores = new ConcurrentBag<RestoreFailure>();
        var processedCount = 0;

        // Use semaphore for concurrency control
        using var semaphore = new SemaphoreSlim(options.MaxConcurrency);

        var tasks = handleList.Select(async handle =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await RestoreStreamAsync(handle, options, null, cancellationToken);
                successfulRestores.Add(handle.BackupId);
            }
            catch (Exception ex) when (options.ContinueOnError)
            {
                failedRestores.Add(new RestoreFailure
                {
                    BackupId = handle.BackupId,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
            }
            finally
            {
                semaphore.Release();
                var current = Interlocked.Increment(ref processedCount);
                options.OnProgress?.Invoke(new BulkRestoreProgress
                {
                    TotalBackups = handleList.Count,
                    ProcessedBackups = current,
                    SuccessfulRestores = successfulRestores.Count,
                    FailedRestores = failedRestores.Count,
                    CurrentBackupId = handle.BackupId
                });
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var result = new BulkRestoreResult
        {
            TotalProcessed = handleList.Count,
            SuccessCount = successfulRestores.Count,
            FailureCount = failedRestores.Count,
            SuccessfulRestores = successfulRestores.ToList(),
            FailedRestores = failedRestores.ToList(),
            ElapsedTime = stopwatch.Elapsed
        };

        logger.BulkRestoreCompleted(result.SuccessCount, result.FailureCount, stopwatch.Elapsed);

        return result;
    }

    #endregion

    #region Backup Management

    /// <inheritdoc/>
    public async Task<IEnumerable<IBackupHandle>> ListBackupsAsync(
        BackupQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        if (backupRegistry == null)
        {
            throw new InvalidOperationException(
                "Backup registry is not configured. Cannot list backups without a registry.");
        }

        return await backupRegistry.QueryBackupsAsync(query, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IBackupHandle?> GetBackupAsync(
        Guid backupId,
        CancellationToken cancellationToken = default)
    {
        if (backupRegistry == null)
        {
            throw new InvalidOperationException(
                "Backup registry is not configured. Cannot get backup by ID without a registry.");
        }

        return await backupRegistry.GetBackupAsync(backupId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> ValidateBackupAsync(
        IBackupHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        return backupProvider.ValidateBackupAsync(handle, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteBackupAsync(
        IBackupHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        logger.DeleteBackupStarting(handle.BackupId);

        // Delete from provider
        await backupProvider.DeleteBackupAsync(handle, cancellationToken);

        // Unregister from registry if available
        if (backupRegistry != null)
        {
            await backupRegistry.UnregisterBackupAsync(handle.BackupId, cancellationToken);
        }

        logger.DeleteBackupCompleted(handle.BackupId);
    }

    /// <inheritdoc/>
    public async Task<int> CleanupExpiredBackupsAsync(
        CancellationToken cancellationToken = default)
    {
        if (backupRegistry == null)
        {
            throw new InvalidOperationException(
                "Backup registry is not configured. Cannot cleanup expired backups without a registry.");
        }

        logger.CleanupExpiredBackupsStarting();

        // Query for expired backups
        var expiredBackups = await backupRegistry.GetExpiredBackupsAsync(cancellationToken);
        var expiredList = expiredBackups.ToList();

        if (expiredList.Count == 0)
        {
            logger.NoExpiredBackupsFound();
            return 0;
        }

        var deletedCount = 0;
        foreach (var handle in expiredList)
        {
            try
            {
                await DeleteBackupAsync(handle, cancellationToken);
                deletedCount++;
            }
            catch (Exception ex)
            {
                logger.FailedToDeleteExpiredBackup(handle.BackupId, ex);
            }
        }

        logger.CleanupExpiredBackupsCompleted(deletedCount);

        return deletedCount;
    }

    #endregion
}

/// <summary>
/// Logging extensions for BackupRestoreService.
/// </summary>
internal static partial class BackupRestoreServiceLoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Starting backup of stream {ObjectName}/{ObjectId}")]
    public static partial void BackupStreamStarting(this ILogger logger, string objectName, string objectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting backup of document {ObjectName}/{ObjectId}")]
    public static partial void BackupDocumentStarting(this ILogger logger, string objectName, string objectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup {BackupId} completed for {ObjectId} with {EventCount} events")]
    public static partial void BackupCompleted(this ILogger logger, Guid backupId, string objectId, int eventCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting restore of backup {BackupId} to {ObjectId}")]
    public static partial void RestoreStarting(this ILogger logger, Guid backupId, string objectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restore of backup {BackupId} to {ObjectId} completed")]
    public static partial void RestoreCompleted(this ILogger logger, Guid backupId, string objectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting restore of backup {BackupId} from {SourceObjectId} to new stream {TargetObjectId}")]
    public static partial void RestoreToNewStreamStarting(this ILogger logger, Guid backupId, string sourceObjectId, string targetObjectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restore of backup {BackupId} to new stream {TargetObjectId} completed")]
    public static partial void RestoreToNewStreamCompleted(this ILogger logger, Guid backupId, string targetObjectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting bulk backup of {Count} {ObjectName} streams with concurrency {MaxConcurrency}")]
    public static partial void BulkBackupStarting(this ILogger logger, string objectName, int count, int maxConcurrency);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bulk backup completed: {SuccessCount} succeeded, {FailureCount} failed in {ElapsedTime}")]
    public static partial void BulkBackupCompleted(this ILogger logger, int successCount, int failureCount, TimeSpan elapsedTime);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting bulk restore of {Count} backups with concurrency {MaxConcurrency}")]
    public static partial void BulkRestoreStarting(this ILogger logger, int count, int maxConcurrency);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bulk restore completed: {SuccessCount} succeeded, {FailureCount} failed in {ElapsedTime}")]
    public static partial void BulkRestoreCompleted(this ILogger logger, int successCount, int failureCount, TimeSpan elapsedTime);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting deletion of backup {BackupId}")]
    public static partial void DeleteBackupStarting(this ILogger logger, Guid backupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup {BackupId} deleted")]
    public static partial void DeleteBackupCompleted(this ILogger logger, Guid backupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting cleanup of expired backups")]
    public static partial void CleanupExpiredBackupsStarting(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "No expired backups found")]
    public static partial void NoExpiredBackupsFound(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete expired backup {BackupId}")]
    public static partial void FailedToDeleteExpiredBackup(this ILogger logger, Guid backupId, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleanup completed: {DeletedCount} expired backups deleted")]
    public static partial void CleanupExpiredBackupsCompleted(this ILogger logger, int deletedCount);
}
