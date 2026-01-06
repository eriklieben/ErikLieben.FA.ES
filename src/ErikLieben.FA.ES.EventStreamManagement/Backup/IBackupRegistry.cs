namespace ErikLieben.FA.ES.EventStreamManagement.Backup;

/// <summary>
/// Interface for tracking and querying backups.
/// </summary>
public interface IBackupRegistry
{
    /// <summary>
    /// Registers a new backup in the registry.
    /// </summary>
    /// <param name="handle">The backup handle to register.</param>
    /// <param name="options">The options used for the backup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RegisterBackupAsync(
        IBackupHandle handle,
        BackupOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a backup by its ID.
    /// </summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The backup handle if found; otherwise null.</returns>
    Task<IBackupHandle?> GetBackupAsync(
        Guid backupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries backups matching the specified criteria.
    /// </summary>
    /// <param name="query">Query to filter backups. If null, returns all backups.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of matching backup handles.</returns>
    Task<IEnumerable<IBackupHandle>> QueryBackupsAsync(
        BackupQuery? query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a backup from the registry.
    /// </summary>
    /// <param name="backupId">The backup identifier to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnregisterBackupAsync(
        Guid backupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all backups that have exceeded their retention period.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of expired backup handles.</returns>
    Task<IEnumerable<IBackupHandle>> GetExpiredBackupsAsync(
        CancellationToken cancellationToken = default);
}
