namespace ErikLieben.FA.ES.EventStreamManagement.Backup;

using ErikLieben.FA.ES.Documents;

/// <summary>
/// Provides backup and restore capabilities for event streams.
/// </summary>
public interface IBackupProvider
{
    /// <summary>
    /// Gets the name of this backup provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Creates a backup of an event stream and related data.
    /// </summary>
    /// <param name="context">The backup context.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A handle to the created backup.</returns>
    Task<IBackupHandle> BackupAsync(
        BackupContext context,
        IProgress<BackupProgress>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores an event stream from a backup.
    /// </summary>
    /// <param name="handle">The backup handle.</param>
    /// <param name="context">The restore context.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RestoreAsync(
        IBackupHandle handle,
        RestoreContext context,
        IProgress<RestoreProgress>? progress,
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
}

/// <summary>
/// Context for backup operations.
/// </summary>
public class BackupContext
{
    /// <summary>
    /// Gets or sets the object document to backup.
    /// </summary>
    public required IObjectDocument Document { get; set; }

    /// <summary>
    /// Gets or sets the backup configuration.
    /// </summary>
    public required Core.BackupConfiguration Configuration { get; set; }

    /// <summary>
    /// Gets or sets events to backup.
    /// </summary>
    public IEnumerable<IEvent>? Events { get; set; }
}

/// <summary>
/// Context for restore operations.
/// </summary>
public class RestoreContext
{
    /// <summary>
    /// Gets or sets the target object document.
    /// </summary>
    public required IObjectDocument TargetDocument { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to overwrite existing data.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    /// Gets or sets the data store to use for writing restored events.
    /// </summary>
    public EventStream.IDataStore? DataStore { get; set; }
}

/// <summary>
/// Reports backup progress.
/// </summary>
public class BackupProgress
{
    /// <summary>
    /// Gets or sets the number of events backed up.
    /// </summary>
    public long EventsBackedUp { get; set; }

    /// <summary>
    /// Gets or sets the total number of events to backup.
    /// </summary>
    public long TotalEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes written.
    /// </summary>
    public long BytesWritten { get; set; }

    /// <summary>
    /// Gets or sets the percentage complete.
    /// </summary>
    public double PercentageComplete => TotalEvents > 0
        ? (double)EventsBackedUp / TotalEvents * 100.0
        : 0.0;
}

/// <summary>
/// Reports restore progress.
/// </summary>
public class RestoreProgress
{
    /// <summary>
    /// Gets or sets the number of events restored.
    /// </summary>
    public long EventsRestored { get; set; }

    /// <summary>
    /// Gets or sets the total number of events to restore.
    /// </summary>
    public long TotalEvents { get; set; }

    /// <summary>
    /// Gets or sets the percentage complete.
    /// </summary>
    public double PercentageComplete => TotalEvents > 0
        ? (double)EventsRestored / TotalEvents * 100.0
        : 0.0;
}
