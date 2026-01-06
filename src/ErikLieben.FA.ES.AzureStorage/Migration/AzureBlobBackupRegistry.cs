namespace ErikLieben.FA.ES.AzureStorage.Migration;

using Azure.Storage.Blobs;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IBackupRegistry"/>.
/// Stores backup metadata in a registry blob for querying and tracking.
/// </summary>
public class AzureBlobBackupRegistry : IBackupRegistry
{
    private const string RegistryBlobName = "backup-registry.json";
    private readonly BlobServiceClient blobServiceClient;
    private readonly ILogger<AzureBlobBackupRegistry> logger;
    private readonly string containerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobBackupRegistry"/> class.
    /// </summary>
    /// <param name="blobServiceClient">The blob service client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="containerName">Container name for registry storage.</param>
    public AzureBlobBackupRegistry(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobBackupRegistry> logger,
        string containerName = "backup-registry")
    {
        this.blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.containerName = containerName;
    }

    /// <inheritdoc/>
    public async Task RegisterBackupAsync(
        IBackupHandle handle,
        BackupOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(options);

        logger.RegisteringBackup(handle.BackupId);

        var registry = await LoadRegistryAsync(cancellationToken);

        var entry = new BackupRegistryEntry
        {
            BackupId = handle.BackupId,
            CreatedAt = handle.CreatedAt,
            ProviderName = handle.ProviderName,
            Location = handle.Location,
            ObjectId = handle.ObjectId,
            ObjectName = handle.ObjectName,
            StreamVersion = handle.StreamVersion,
            EventCount = handle.EventCount,
            SizeBytes = handle.SizeBytes,
            IncludesSnapshots = handle.Metadata.IncludesSnapshots,
            IncludesObjectDocument = handle.Metadata.IncludesObjectDocument,
            IncludesTerminatedStreams = handle.Metadata.IncludesTerminatedStreams,
            IsCompressed = handle.Metadata.IsCompressed,
            Checksum = handle.Metadata.Checksum,
            Retention = options.Retention,
            Description = options.Description,
            Tags = options.Tags
        };

        registry.Entries.Add(entry);
        await SaveRegistryAsync(registry, cancellationToken);

        logger.BackupRegistered(handle.BackupId);
    }

    /// <inheritdoc/>
    public async Task<IBackupHandle?> GetBackupAsync(
        Guid backupId,
        CancellationToken cancellationToken = default)
    {
        var registry = await LoadRegistryAsync(cancellationToken);
        var entry = registry.Entries.FirstOrDefault(e => e.BackupId == backupId);
        return entry?.ToBackupHandle();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IBackupHandle>> QueryBackupsAsync(
        BackupQuery? query,
        CancellationToken cancellationToken = default)
    {
        var registry = await LoadRegistryAsync(cancellationToken);
        var results = registry.Entries.AsEnumerable();

        if (query != null)
        {
            if (!string.IsNullOrEmpty(query.ObjectName))
            {
                results = results.Where(e => e.ObjectName == query.ObjectName);
            }

            if (!string.IsNullOrEmpty(query.ObjectId))
            {
                results = results.Where(e => e.ObjectId == query.ObjectId);
            }

            if (query.CreatedAfter.HasValue)
            {
                results = results.Where(e => e.CreatedAt >= query.CreatedAfter.Value);
            }

            if (query.CreatedBefore.HasValue)
            {
                results = results.Where(e => e.CreatedAt <= query.CreatedBefore.Value);
            }

            if (query.Tags != null && query.Tags.Count > 0)
            {
                results = results.Where(e =>
                    e.Tags != null &&
                    query.Tags.All(qt => e.Tags.TryGetValue(qt.Key, out var value) && value == qt.Value));
            }

            if (!query.IncludeExpired)
            {
                var now = DateTimeOffset.UtcNow;
                results = results.Where(e =>
                    !e.Retention.HasValue || e.CreatedAt.Add(e.Retention.Value) > now);
            }

            if (query.MaxResults.HasValue)
            {
                results = results.Take(query.MaxResults.Value);
            }
        }

        return results.Select(e => e.ToBackupHandle()).ToList();
    }

    /// <inheritdoc/>
    public async Task UnregisterBackupAsync(
        Guid backupId,
        CancellationToken cancellationToken = default)
    {
        logger.UnregisteringBackup(backupId);

        var registry = await LoadRegistryAsync(cancellationToken);
        var removed = registry.Entries.RemoveAll(e => e.BackupId == backupId);

        if (removed > 0)
        {
            await SaveRegistryAsync(registry, cancellationToken);
            logger.BackupUnregistered(backupId);
        }
        else
        {
            logger.BackupNotFoundInRegistry(backupId);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IBackupHandle>> GetExpiredBackupsAsync(
        CancellationToken cancellationToken = default)
    {
        var registry = await LoadRegistryAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var expired = registry.Entries
            .Where(e => e.Retention.HasValue && e.CreatedAt.Add(e.Retention.Value) <= now)
            .Select(e => e.ToBackupHandle())
            .ToList();

        logger.FoundExpiredBackups(expired.Count);

        return expired;
    }

    private async Task<BackupRegistryData> LoadRegistryAsync(CancellationToken cancellationToken)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        if (!await containerClient.ExistsAsync(cancellationToken))
        {
            return new BackupRegistryData();
        }

        var blobClient = containerClient.GetBlobClient(RegistryBlobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return new BackupRegistryData();
        }

        var response = await blobClient.DownloadContentAsync(cancellationToken);
        var json = Encoding.UTF8.GetString(response.Value.Content.ToArray());

        return JsonSerializer.Deserialize(json, MigrationJsonContext.Default.BackupRegistryData)
            ?? new BackupRegistryData();
    }

    private async Task SaveRegistryAsync(BackupRegistryData registry, CancellationToken cancellationToken)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(RegistryBlobName);
        var json = JsonSerializer.Serialize(registry, MigrationJsonContext.Default.BackupRegistryData);
        var bytes = Encoding.UTF8.GetBytes(json);

        await blobClient.UploadAsync(
            new BinaryData(bytes),
            overwrite: true,
            cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Data structure for the backup registry.
/// </summary>
internal class BackupRegistryData
{
    /// <summary>
    /// Gets or sets the list of backup entries.
    /// </summary>
    public List<BackupRegistryEntry> Entries { get; set; } = [];
}

/// <summary>
/// A backup entry in the registry.
/// </summary>
internal class BackupRegistryEntry
{
    public Guid BackupId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public int StreamVersion { get; set; }
    public int EventCount { get; set; }
    public long SizeBytes { get; set; }
    public bool IncludesSnapshots { get; set; }
    public bool IncludesObjectDocument { get; set; }
    public bool IncludesTerminatedStreams { get; set; }
    public bool IsCompressed { get; set; }
    public string? Checksum { get; set; }
    public TimeSpan? Retention { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string>? Tags { get; set; }

    public IBackupHandle ToBackupHandle()
    {
        return new RegistryBackupHandle
        {
            BackupId = BackupId,
            CreatedAt = CreatedAt,
            ProviderName = ProviderName,
            Location = Location,
            ObjectId = ObjectId,
            ObjectName = ObjectName,
            StreamVersion = StreamVersion,
            EventCount = EventCount,
            SizeBytes = SizeBytes,
            Metadata = new BackupMetadata
            {
                IncludesSnapshots = IncludesSnapshots,
                IncludesObjectDocument = IncludesObjectDocument,
                IncludesTerminatedStreams = IncludesTerminatedStreams,
                IsCompressed = IsCompressed,
                Checksum = Checksum
            }
        };
    }
}

/// <summary>
/// Backup handle implementation for registry entries.
/// </summary>
internal class RegistryBackupHandle : IBackupHandle
{
    public Guid BackupId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public required string ProviderName { get; init; }
    public required string Location { get; init; }
    public required BackupMetadata Metadata { get; init; }
    public required string ObjectId { get; init; }
    public required string ObjectName { get; init; }
    public int StreamVersion { get; init; }
    public int EventCount { get; init; }
    public long SizeBytes { get; init; }
}

/// <summary>
/// Logging extensions for AzureBlobBackupRegistry.
/// </summary>
internal static partial class AzureBlobBackupRegistryLoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Registering backup {BackupId} in registry")]
    public static partial void RegisteringBackup(this ILogger logger, Guid backupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup {BackupId} registered successfully")]
    public static partial void BackupRegistered(this ILogger logger, Guid backupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unregistering backup {BackupId} from registry")]
    public static partial void UnregisteringBackup(this ILogger logger, Guid backupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup {BackupId} unregistered from registry")]
    public static partial void BackupUnregistered(this ILogger logger, Guid backupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Backup {BackupId} not found in registry")]
    public static partial void BackupNotFoundInRegistry(this ILogger logger, Guid backupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} expired backups")]
    public static partial void FoundExpiredBackups(this ILogger logger, int count);
}
