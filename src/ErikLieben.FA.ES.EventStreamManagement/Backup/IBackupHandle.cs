namespace ErikLieben.FA.ES.EventStreamManagement.Backup;

/// <summary>
/// Represents a handle to a backup with its metadata.
/// </summary>
public interface IBackupHandle
{
    /// <summary>
    /// Gets the unique identifier for this backup.
    /// </summary>
    Guid BackupId { get; }

    /// <summary>
    /// Gets the timestamp when the backup was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the name of the provider that created this backup.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the location where the backup is stored.
    /// </summary>
    string Location { get; }

    /// <summary>
    /// Gets metadata about the backup.
    /// </summary>
    BackupMetadata Metadata { get; }

    /// <summary>
    /// Gets the object identifier that was backed up.
    /// </summary>
    string ObjectId { get; }

    /// <summary>
    /// Gets the object name that was backed up.
    /// </summary>
    string ObjectName { get; }

    /// <summary>
    /// Gets the stream version at the time of backup.
    /// </summary>
    int StreamVersion { get; }

    /// <summary>
    /// Gets the number of events in the backup.
    /// </summary>
    int EventCount { get; }

    /// <summary>
    /// Gets the size of the backup in bytes.
    /// </summary>
    long SizeBytes { get; }
}

/// <summary>
/// Metadata about a backup.
/// </summary>
public class BackupMetadata
{
    /// <summary>
    /// Gets or sets a value indicating whether snapshots were included.
    /// </summary>
    public bool IncludesSnapshots { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the object document was included.
    /// </summary>
    public bool IncludesObjectDocument { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether terminated streams were included.
    /// </summary>
    public bool IncludesTerminatedStreams { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the backup is compressed.
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Gets or sets the checksum of the backup for integrity verification.
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Gets or sets custom metadata.
    /// </summary>
    public Dictionary<string, string> Custom { get; set; } = new();
}
