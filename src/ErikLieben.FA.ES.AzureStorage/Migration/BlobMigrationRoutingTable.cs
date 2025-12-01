namespace ErikLieben.FA.ES.AzureStorage.Migration;

using Azure;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

/// <summary>
/// Implementation of migration routing table using Azure Blob Storage.
/// </summary>
public class BlobMigrationRoutingTable : IMigrationRoutingTable
{
    private readonly BlobServiceClient blobServiceClient;
    private readonly ILogger<BlobMigrationRoutingTable> logger;
    private readonly string containerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobMigrationRoutingTable"/> class.
    /// </summary>
    public BlobMigrationRoutingTable(
        BlobServiceClient blobServiceClient,
        ILogger<BlobMigrationRoutingTable> logger,
        string containerName = "migration-routing")
    {
        this.blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.containerName = containerName;
    }

    /// <inheritdoc/>
    public async Task<MigrationPhase> GetPhaseAsync(
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var routing = await GetRoutingAsync(objectId, cancellationToken);
        return routing.Phase;
    }

    /// <inheritdoc/>
    public async Task<StreamRouting> GetRoutingAsync(
        string objectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectId);

        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(GetBlobName(objectId));

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                // No routing entry means normal operations
                return StreamRouting.Normal(string.Empty);
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var entry = JsonSerializer.Deserialize(
                response.Value.Content.ToString(),
                MigrationJsonContext.Default.MigrationRoutingEntry);

            if (entry == null)
            {
                logger.LogWarning(
                    "Failed to deserialize routing entry for {ObjectId}",
                    objectId);
                return StreamRouting.Normal(string.Empty);
            }

            logger.LogDebug(
                "Retrieved routing for {ObjectId}: Phase={Phase}, Old={OldStream}, New={NewStream}",
                objectId,
                entry.Phase,
                entry.OldStream,
                entry.NewStream);

            return entry.ToStreamRouting();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return StreamRouting.Normal(string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error retrieving routing for {ObjectId}",
                objectId);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SetMigrationPhaseAsync(
        string objectId,
        MigrationPhase phase,
        string oldStream,
        string newStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectId);
        ArgumentNullException.ThrowIfNull(oldStream);
        ArgumentNullException.ThrowIfNull(newStream);

        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(GetBlobName(objectId));

            // Get existing entry or create new
            MigrationRoutingEntry entry;
            if (await blobClient.ExistsAsync(cancellationToken))
            {
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                entry = JsonSerializer.Deserialize(
                    response.Value.Content.ToString(),
                    MigrationJsonContext.Default.MigrationRoutingEntry) ?? new MigrationRoutingEntry();
            }
            else
            {
                entry = new MigrationRoutingEntry
                {
                    ObjectId = objectId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    MigrationId = Guid.NewGuid()
                };
            }

            // Update entry
            entry.Phase = phase;
            entry.OldStream = oldStream;
            entry.NewStream = newStream;
            entry.UpdatedAt = DateTimeOffset.UtcNow;

            // Serialize and upload
            var json = JsonSerializer.Serialize(entry, MigrationJsonContext.Default.MigrationRoutingEntry);
            var content = new BinaryData(Encoding.UTF8.GetBytes(json));

            await blobClient.UploadAsync(
                content,
                overwrite: true,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Set routing for {ObjectId}: Phase={Phase}, Old={OldStream}, New={NewStream}",
                objectId,
                phase,
                oldStream,
                newStream);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error setting routing for {ObjectId}",
                objectId);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RemoveRoutingAsync(string objectId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectId);

        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(GetBlobName(objectId));

            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            logger.LogInformation(
                "Removed routing for {ObjectId}",
                objectId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error removing routing for {ObjectId}",
                objectId);

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetActiveMigrationsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            if (!await containerClient.ExistsAsync(cancellationToken))
            {
                return Array.Empty<string>();
            }

            var objectIds = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                if (blobItem.Name.EndsWith(".routing.json"))
                {
                    var objectId = blobItem.Name.Replace(".routing.json", "");
                    objectIds.Add(objectId);
                }
            }

            return objectIds;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error retrieving active migrations");

            throw;
        }
    }

    private static string GetBlobName(string objectId)
    {
        return $"{objectId}.routing.json";
    }
}
