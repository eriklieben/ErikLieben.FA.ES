namespace ErikLieben.FA.ES.EventStreamManagement.Backup;

/// <summary>
/// Options for a single-stream backup operation.
/// </summary>
public class BackupOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to include snapshots in the backup.
    /// </summary>
    public bool IncludeSnapshots { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include the object document in the backup.
    /// </summary>
    public bool IncludeObjectDocument { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include terminated streams in the backup.
    /// </summary>
    public bool IncludeTerminatedStreams { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to compress the backup.
    /// </summary>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Gets or sets the backup location/container. If null, uses provider default.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the retention period for the backup.
    /// After this period, the backup may be automatically cleaned up.
    /// </summary>
    public TimeSpan? Retention { get; set; }

    /// <summary>
    /// Gets or sets a description or reason for the backup.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets custom tags for categorizing the backup.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Creates a default backup options instance with standard settings.
    /// </summary>
    public static BackupOptions Default => new()
    {
        IncludeSnapshots = false,
        IncludeObjectDocument = true,
        IncludeTerminatedStreams = false,
        EnableCompression = true
    };

    /// <summary>
    /// Creates a full backup options instance that includes all data.
    /// </summary>
    public static BackupOptions Full => new()
    {
        IncludeSnapshots = true,
        IncludeObjectDocument = true,
        IncludeTerminatedStreams = true,
        EnableCompression = true
    };

    /// <summary>
    /// Converts these options to a <see cref="Core.BackupConfiguration"/> for use with backup providers.
    /// </summary>
    internal Core.BackupConfiguration ToConfiguration()
    {
        return new Core.BackupConfiguration
        {
            IncludeSnapshots = IncludeSnapshots,
            IncludeObjectDocument = IncludeObjectDocument,
            IncludeTerminatedStreams = IncludeTerminatedStreams,
            EnableCompression = EnableCompression,
            Location = Location,
            Retention = Retention
        };
    }
}

/// <summary>
/// Options for a single-stream restore operation.
/// </summary>
public class RestoreOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to overwrite the existing stream if it exists.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate the backup before restoring.
    /// </summary>
    public bool ValidateBeforeRestore { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to restore the object document.
    /// Only applies if the backup includes the object document.
    /// </summary>
    public bool RestoreObjectDocument { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to restore snapshots.
    /// Only applies if the backup includes snapshots.
    /// </summary>
    public bool RestoreSnapshots { get; set; } = true;

    /// <summary>
    /// Gets or sets a description or reason for the restore operation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Creates a default restore options instance.
    /// </summary>
    public static RestoreOptions Default => new()
    {
        Overwrite = false,
        ValidateBeforeRestore = true,
        RestoreObjectDocument = true,
        RestoreSnapshots = true
    };

    /// <summary>
    /// Creates restore options that will overwrite existing data.
    /// </summary>
    public static RestoreOptions WithOverwrite => new()
    {
        Overwrite = true,
        ValidateBeforeRestore = true,
        RestoreObjectDocument = true,
        RestoreSnapshots = true
    };

    /// <summary>
    /// Converts these options to a <see cref="RestoreContext"/> for use with backup providers.
    /// </summary>
    /// <param name="targetDocument">The target document to restore to.</param>
    /// <param name="dataStore">Optional data store for writing events.</param>
    internal RestoreContext ToContext(
        Documents.IObjectDocument targetDocument,
        EventStream.IDataStore? dataStore = null)
    {
        return new RestoreContext
        {
            TargetDocument = targetDocument,
            Overwrite = Overwrite,
            DataStore = dataStore
        };
    }
}

/// <summary>
/// Options for bulk backup operations.
/// </summary>
public class BulkBackupOptions : BackupOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent backup operations.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether to continue processing remaining items
    /// when one backup fails.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Gets or sets a callback for progress reporting during bulk operations.
    /// </summary>
    public Action<BulkBackupProgress>? OnProgress { get; set; }

    /// <summary>
    /// Creates default bulk backup options.
    /// </summary>
    public new static BulkBackupOptions Default => new()
    {
        IncludeSnapshots = false,
        IncludeObjectDocument = true,
        IncludeTerminatedStreams = false,
        EnableCompression = true,
        MaxConcurrency = 4,
        ContinueOnError = true
    };
}

/// <summary>
/// Options for bulk restore operations.
/// </summary>
public class BulkRestoreOptions : RestoreOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent restore operations.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether to continue processing remaining items
    /// when one restore fails.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Gets or sets a callback for progress reporting during bulk operations.
    /// </summary>
    public Action<BulkRestoreProgress>? OnProgress { get; set; }

    /// <summary>
    /// Creates default bulk restore options.
    /// </summary>
    public new static BulkRestoreOptions Default => new()
    {
        Overwrite = false,
        ValidateBeforeRestore = true,
        RestoreObjectDocument = true,
        RestoreSnapshots = true,
        MaxConcurrency = 4,
        ContinueOnError = true
    };
}

/// <summary>
/// Progress information for bulk backup operations.
/// </summary>
public class BulkBackupProgress
{
    /// <summary>
    /// Gets or sets the total number of streams to backup.
    /// </summary>
    public int TotalStreams { get; set; }

    /// <summary>
    /// Gets or sets the number of streams that have been processed (success or failure).
    /// </summary>
    public int ProcessedStreams { get; set; }

    /// <summary>
    /// Gets or sets the number of successful backups.
    /// </summary>
    public int SuccessfulBackups { get; set; }

    /// <summary>
    /// Gets or sets the number of failed backups.
    /// </summary>
    public int FailedBackups { get; set; }

    /// <summary>
    /// Gets or sets the current stream being processed.
    /// </summary>
    public string? CurrentStreamId { get; set; }

    /// <summary>
    /// Gets the percentage of completion.
    /// </summary>
    public double PercentageComplete => TotalStreams > 0
        ? (double)ProcessedStreams / TotalStreams * 100.0
        : 0.0;
}

/// <summary>
/// Progress information for bulk restore operations.
/// </summary>
public class BulkRestoreProgress
{
    /// <summary>
    /// Gets or sets the total number of backups to restore.
    /// </summary>
    public int TotalBackups { get; set; }

    /// <summary>
    /// Gets or sets the number of backups that have been processed (success or failure).
    /// </summary>
    public int ProcessedBackups { get; set; }

    /// <summary>
    /// Gets or sets the number of successful restores.
    /// </summary>
    public int SuccessfulRestores { get; set; }

    /// <summary>
    /// Gets or sets the number of failed restores.
    /// </summary>
    public int FailedRestores { get; set; }

    /// <summary>
    /// Gets or sets the current backup being restored.
    /// </summary>
    public Guid? CurrentBackupId { get; set; }

    /// <summary>
    /// Gets the percentage of completion.
    /// </summary>
    public double PercentageComplete => TotalBackups > 0
        ? (double)ProcessedBackups / TotalBackups * 100.0
        : 0.0;
}

/// <summary>
/// Query options for listing backups.
/// </summary>
public class BackupQuery
{
    /// <summary>
    /// Gets or sets the object name to filter by.
    /// </summary>
    public string? ObjectName { get; set; }

    /// <summary>
    /// Gets or sets the object ID to filter by.
    /// </summary>
    public string? ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the minimum creation date to filter by.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>
    /// Gets or sets the maximum creation date to filter by.
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>
    /// Gets or sets tags that backups must have.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results to return.
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include expired backups.
    /// </summary>
    public bool IncludeExpired { get; set; }
}
