namespace ErikLieben.FA.ES.AzureStorage.Migration;

using Azure.Storage.Blobs;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

/// <summary>
/// Backup provider that stores backups in Azure Blob Storage.
/// </summary>
public class AzureBlobBackupProvider : IBackupProvider
{
    private readonly BlobServiceClient blobServiceClient;
    private readonly ILogger<AzureBlobBackupProvider> logger;
    private readonly string containerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobBackupProvider"/> class.
    /// </summary>
    public AzureBlobBackupProvider(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobBackupProvider> logger,
        string containerName = "migration-backups")
    {
        this.blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.containerName = containerName;
    }

    /// <inheritdoc/>
    public string ProviderName => "azure-blob";

    /// <inheritdoc/>
    public async Task<IBackupHandle> BackupAsync(
        BackupContext context,
        IProgress<BackupProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var backupId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        logger.BackupStarting(backupId, context.Document.ObjectId);

        // Ensure container exists
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Create backup structure
        var backup = new BackupData
        {
            BackupId = backupId,
            CreatedAt = timestamp,
            ObjectId = context.Document.ObjectId,
            ObjectName = context.Document.ObjectName,
            StreamVersion = context.Document.Active.CurrentStreamVersion
        };

        // Backup events - serialize full events using AOT-compatible JsonEventSerializerContext
        if (context.Events != null)
        {
            var eventList = context.Events.ToList();
            backup.EventCount = eventList.Count;
            backup.SerializedEvents = eventList
                .Select(e =>
                {
                    // Convert to JsonEvent for serialization (handles both JsonEvent and other IEvent implementations)
                    var jsonEvent = e as JsonEvent ?? new JsonEvent
                    {
                        EventType = e.EventType,
                        EventVersion = e.EventVersion,
                        SchemaVersion = e.SchemaVersion,
                        Payload = e.Payload,
                        ExternalSequencer = e.ExternalSequencer,
                        ActionMetadata = e.ActionMetadata ?? new ActionMetadata(),
                        Metadata = e.Metadata ?? []
                    };
                    return JsonSerializer.Serialize(jsonEvent, JsonEventSerializerContext.Default.JsonEvent);
                })
                .ToList();
        }

        // Serialize to JSON using source-generated context
        var json = JsonSerializer.Serialize(backup, MigrationJsonContext.Default.BackupData);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Compress if configured
        if (context.Configuration.EnableCompression)
        {
            using var outputStream = new MemoryStream();
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            {
                await gzipStream.WriteAsync(bytes, cancellationToken);
            }
            bytes = outputStream.ToArray();
        }

        // Upload to blob storage
        var blobName = GetBackupBlobName(backupId, context.Document.ObjectId);
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            new BinaryData(bytes),
            overwrite: false,
            cancellationToken: cancellationToken);

        logger.BackupCompleted(backupId, bytes.Length);

        // Create and return handle
        return new BlobBackupHandle
        {
            BackupId = backupId,
            CreatedAt = timestamp,
            ProviderName = ProviderName,
            Location = blobClient.Uri.ToString(),
            ObjectId = context.Document.ObjectId,
            ObjectName = context.Document.ObjectName,
            StreamVersion = context.Document.Active.CurrentStreamVersion,
            EventCount = backup.EventCount,
            SizeBytes = bytes.Length,
            Metadata = new BackupMetadata
            {
                IncludesSnapshots = context.Configuration.IncludeSnapshots,
                IncludesObjectDocument = context.Configuration.IncludeObjectDocument,
                IncludesTerminatedStreams = context.Configuration.IncludeTerminatedStreams,
                IsCompressed = context.Configuration.EnableCompression
            }
        };
    }

    /// <inheritdoc/>
    public async Task RestoreAsync(
        IBackupHandle handle,
        RestoreContext context,
        IProgress<RestoreProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(context);

        logger.RestoreStarting(handle.BackupId);

        // Download backup blob
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobName = GetBackupBlobName(handle.BackupId, handle.ObjectId);
        var blobClient = containerClient.GetBlobClient(blobName);

        var response = await blobClient.DownloadContentAsync(cancellationToken);
        var bytes = response.Value.Content.ToArray();

        // Decompress if needed
        if (handle.Metadata.IsCompressed)
        {
            using var inputStream = new MemoryStream(bytes);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            await gzipStream.CopyToAsync(outputStream, cancellationToken);
            bytes = outputStream.ToArray();
        }

        // Deserialize using source-generated context
        var json = Encoding.UTF8.GetString(bytes);
        var backup = JsonSerializer.Deserialize(json, MigrationJsonContext.Default.BackupData);

        if (backup == null)
        {
            throw new InvalidOperationException("Failed to deserialize backup data");
        }

        // Validate data store is provided
        if (context.DataStore == null)
        {
            throw new InvalidOperationException("DataStore is required in RestoreContext to restore events");
        }

        // Deserialize events from backup
        var events = new List<IEvent>();
        if (backup.SerializedEvents != null)
        {
            foreach (var serializedEvent in backup.SerializedEvents)
            {
                var jsonEvent = JsonSerializer.Deserialize(serializedEvent, JsonEventSerializerContext.Default.JsonEvent);
                if (jsonEvent != null)
                {
                    events.Add(jsonEvent);
                }
            }
        }

        // Report initial progress
        progress?.Report(new RestoreProgress
        {
            EventsRestored = 0,
            TotalEvents = events.Count
        });

        // Write events to data store with preserved timestamps
        if (events.Count > 0)
        {
            await context.DataStore.AppendAsync(
                context.TargetDocument,
                preserveTimestamp: true,
                CancellationToken.None,
                events.ToArray());
        }

        // Report completion
        progress?.Report(new RestoreProgress
        {
            EventsRestored = events.Count,
            TotalEvents = events.Count
        });

        logger.RestoreCompleted(handle.BackupId, backup.EventCount);
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateBackupAsync(
        IBackupHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobName = GetBackupBlobName(handle.BackupId, handle.ObjectId);
            var blobClient = containerClient.GetBlobClient(blobName);

            // Check if blob exists
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                logger.BackupBlobNotFound(handle.BackupId);
                return false;
            }

            // Download and validate structure
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var bytes = response.Value.Content.ToArray();

            // Decompress if needed
            if (handle.Metadata.IsCompressed)
            {
                using var inputStream = new MemoryStream(bytes);
                using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
                using var outputStream = new MemoryStream();
                await gzipStream.CopyToAsync(outputStream, cancellationToken);
                bytes = outputStream.ToArray();
            }

            // Try to deserialize using source-generated context
            var json = Encoding.UTF8.GetString(bytes);
            var backup = JsonSerializer.Deserialize(json, MigrationJsonContext.Default.BackupData);

            if (backup == null || backup.BackupId != handle.BackupId)
            {
                logger.BackupInvalidStructure(handle.BackupId);
                return false;
            }

            logger.BackupValidationSuccessful(handle.BackupId);

            return true;
        }
        catch (Exception ex)
        {
            logger.BackupValidationError(handle.BackupId, ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteBackupAsync(
        IBackupHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobName = GetBackupBlobName(handle.BackupId, handle.ObjectId);
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        logger.BackupDeleted(handle.BackupId);
    }

    private static string GetBackupBlobName(Guid backupId, string objectId)
    {
        return $"backups/{objectId}/{backupId}.backup.json";
    }
}

/// <summary>
/// Implementation of backup handle for Azure Blob Storage.
/// </summary>
public class BlobBackupHandle : IBackupHandle
{
    /// <inheritdoc/>
    public Guid BackupId { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset CreatedAt { get; init; }

    /// <inheritdoc/>
    public required string ProviderName { get; init; }

    /// <inheritdoc/>
    public required string Location { get; init; }

    /// <inheritdoc/>
    public required BackupMetadata Metadata { get; init; }

    /// <inheritdoc/>
    public required string ObjectId { get; init; }

    /// <inheritdoc/>
    public required string ObjectName { get; init; }

    /// <inheritdoc/>
    public int StreamVersion { get; init; }

    /// <inheritdoc/>
    public int EventCount { get; init; }

    /// <inheritdoc/>
    public long SizeBytes { get; init; }
}
