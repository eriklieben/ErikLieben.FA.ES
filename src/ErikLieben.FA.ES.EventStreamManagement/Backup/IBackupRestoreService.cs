namespace ErikLieben.FA.ES.EventStreamManagement.Backup;

using ErikLieben.FA.ES.Documents;

/// <summary>
/// Provides general-purpose backup and restore capabilities for event streams,
/// independent of migration operations.
/// </summary>
public interface IBackupRestoreService
{
    #region Single Stream Operations

    /// <summary>
    /// Creates a backup of an event stream by object name and ID.
    /// </summary>
    /// <param name="objectName">The object name (aggregate type).</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="options">Backup options. If null, uses default options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A handle to the created backup.</returns>
    Task<IBackupHandle> BackupStreamAsync(
        string objectName,
        string objectId,
        BackupOptions? options = null,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a backup of an event stream using an existing object document.
    /// </summary>
    /// <param name="document">The object document to backup.</param>
    /// <param name="options">Backup options. If null, uses default options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A handle to the created backup.</returns>
    Task<IBackupHandle> BackupDocumentAsync(
        IObjectDocument document,
        BackupOptions? options = null,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores an event stream from a backup to its original location.
    /// </summary>
    /// <param name="handle">The backup handle to restore from.</param>
    /// <param name="options">Restore options. If null, uses default options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestoreStreamAsync(
        IBackupHandle handle,
        RestoreOptions? options = null,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores an event stream from a backup to a new location (different object ID).
    /// </summary>
    /// <param name="handle">The backup handle to restore from.</param>
    /// <param name="targetObjectId">The new object ID to restore to.</param>
    /// <param name="options">Restore options. If null, uses default options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestoreToNewStreamAsync(
        IBackupHandle handle,
        string targetObjectId,
        RestoreOptions? options = null,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Creates backups of multiple event streams.
    /// </summary>
    /// <param name="objectIds">The object identifiers to backup.</param>
    /// <param name="objectName">The object name (aggregate type).</param>
    /// <param name="options">Bulk backup options. If null, uses default options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results of the bulk backup operation.</returns>
    Task<BulkBackupResult> BackupManyAsync(
        IEnumerable<string> objectIds,
        string objectName,
        BulkBackupOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores multiple event streams from their backups.
    /// </summary>
    /// <param name="handles">The backup handles to restore from.</param>
    /// <param name="options">Bulk restore options. If null, uses default options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results of the bulk restore operation.</returns>
    Task<BulkRestoreResult> RestoreManyAsync(
        IEnumerable<IBackupHandle> handles,
        BulkRestoreOptions? options = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Backup Management

    /// <summary>
    /// Lists backups matching the specified query.
    /// </summary>
    /// <param name="query">Query to filter backups. If null, returns all backups.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of matching backup handles.</returns>
    Task<IEnumerable<IBackupHandle>> ListBackupsAsync(
        BackupQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific backup by its ID.
    /// </summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The backup handle if found; otherwise null.</returns>
    Task<IBackupHandle?> GetBackupAsync(
        Guid backupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a backup is intact and can be restored.
    /// </summary>
    /// <param name="handle">The backup handle to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the backup is valid; otherwise false.</returns>
    Task<bool> ValidateBackupAsync(
        IBackupHandle handle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a backup.
    /// </summary>
    /// <param name="handle">The backup handle to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteBackupAsync(
        IBackupHandle handle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up backups that have exceeded their retention period.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of backups deleted.</returns>
    Task<int> CleanupExpiredBackupsAsync(
        CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Result of a bulk backup operation.
/// </summary>
public class BulkBackupResult
{
    /// <summary>
    /// Gets or sets the total number of streams that were processed.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of successful backups.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the number of failed backups.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the handles for successful backups.
    /// </summary>
    public IReadOnlyList<IBackupHandle> SuccessfulBackups { get; set; } = [];

    /// <summary>
    /// Gets or sets information about failed backups.
    /// </summary>
    public IReadOnlyList<BackupFailure> FailedBackups { get; set; } = [];

    /// <summary>
    /// Gets or sets the total elapsed time for the operation.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Gets a value indicating whether all backups were successful.
    /// </summary>
    public bool IsFullySuccessful => FailureCount == 0;

    /// <summary>
    /// Gets a value indicating whether all backups failed.
    /// </summary>
    public bool IsFullyFailed => SuccessCount == 0 && TotalProcessed > 0;

    /// <summary>
    /// Gets a value indicating whether some backups succeeded and some failed.
    /// </summary>
    public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;
}

/// <summary>
/// Information about a failed backup operation.
/// </summary>
public class BackupFailure
{
    /// <summary>
    /// Gets or sets the object ID that failed to backup.
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
    /// Gets or sets the exception that caused the failure.
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Result of a bulk restore operation.
/// </summary>
public class BulkRestoreResult
{
    /// <summary>
    /// Gets or sets the total number of backups that were processed.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of successful restores.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the number of failed restores.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the backup IDs that were successfully restored.
    /// </summary>
    public IReadOnlyList<Guid> SuccessfulRestores { get; set; } = [];

    /// <summary>
    /// Gets or sets information about failed restores.
    /// </summary>
    public IReadOnlyList<RestoreFailure> FailedRestores { get; set; } = [];

    /// <summary>
    /// Gets or sets the total elapsed time for the operation.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Gets a value indicating whether all restores were successful.
    /// </summary>
    public bool IsFullySuccessful => FailureCount == 0;

    /// <summary>
    /// Gets a value indicating whether all restores failed.
    /// </summary>
    public bool IsFullyFailed => SuccessCount == 0 && TotalProcessed > 0;

    /// <summary>
    /// Gets a value indicating whether some restores succeeded and some failed.
    /// </summary>
    public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;
}

/// <summary>
/// Information about a failed restore operation.
/// </summary>
public class RestoreFailure
{
    /// <summary>
    /// Gets or sets the backup ID that failed to restore.
    /// </summary>
    public required Guid BackupId { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public required string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception that caused the failure.
    /// </summary>
    public Exception? Exception { get; set; }
}
