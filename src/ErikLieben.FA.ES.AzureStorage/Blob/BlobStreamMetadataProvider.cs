using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Retention;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IStreamMetadataProvider"/>.
/// Reads blob metadata to determine event count and date ranges for retention evaluation.
/// </summary>
public class BlobStreamMetadataProvider : IStreamMetadataProvider
{
    private readonly IAzureClientFactory<BlobServiceClient> _clientFactory;
    private readonly EventStreamBlobSettings _settings;
    private readonly ILogger<BlobStreamMetadataProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStreamMetadataProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory for creating Blob Service clients.</param>
    /// <param name="settings">The Blob storage settings.</param>
    /// <param name="logger">Optional logger.</param>
    public BlobStreamMetadataProvider(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        EventStreamBlobSettings settings,
        ILogger<BlobStreamMetadataProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        _clientFactory = clientFactory;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StreamMetadata?> GetStreamMetadataAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectName);
        ArgumentNullException.ThrowIfNull(objectId);

        var blobServiceClient = _clientFactory.CreateClient(_settings.DefaultDocumentStore);
        var containerClient = blobServiceClient.GetBlobContainerClient(_settings.DefaultDocumentContainerName);

        try
        {
            var exists = await containerClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return null;
            }

            var objectNameLower = objectName.ToLowerInvariant();
            var prefix = $"{objectNameLower}/{objectId}";
            var eventCount = 0;
            DateTimeOffset? oldest = null;
            DateTimeOffset? newest = null;

#pragma warning disable S3267 // Loops should be simplified - await foreach cannot use LINQ without System.Linq.Async
            await foreach (var blobItem in containerClient.GetBlobsAsync(
                prefix: prefix, cancellationToken: cancellationToken))
            {
                eventCount++;

                if (blobItem.Properties?.CreatedOn != null)
                {
                    var created = blobItem.Properties.CreatedOn.Value;
                    if (oldest == null || created < oldest)
                    {
                        oldest = created;
                    }

                    if (newest == null || created > newest)
                    {
                        newest = created;
                    }
                }
            }
#pragma warning restore S3267

            if (eventCount == 0)
            {
                return null;
            }

            return new StreamMetadata(objectName, objectId, eventCount, oldest, newest);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug(ex, "Container or blob not found for {ObjectName}/{ObjectId}", objectName, objectId);
            }
            return null;
        }
    }
}
