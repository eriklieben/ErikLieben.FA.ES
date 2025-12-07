using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Provides Azure Blob Storage-backed implementation of <see cref="IObjectIdProvider"/>.
/// Uses continuation tokens for efficient pagination through large object collections.
/// </summary>
public class BlobObjectIdProvider : IObjectIdProvider
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly EventStreamBlobSettings blobSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobObjectIdProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="blobSettings">The blob storage settings used for containers.</param>
    public BlobObjectIdProvider(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        EventStreamBlobSettings blobSettings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(blobSettings);

        this.clientFactory = clientFactory;
        this.blobSettings = blobSettings;
    }

    /// <summary>
    /// Gets a page of object IDs for the specified object type using continuation tokens.
    /// </summary>
    /// <param name="objectName">The object type name (e.g., "project", "workItem").</param>
    /// <param name="continuationToken">Optional continuation token from previous page. Pass null for first page.</param>
    /// <param name="pageSize">Number of items to return per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result with object IDs and continuation token for the next page.</returns>
    public async Task<PagedResult<string>> GetObjectIdsAsync(
        string objectName,
        string? continuationToken,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var objectNameLower = objectName.ToLowerInvariant();
        var prefix = $"{objectNameLower}/";
        var items = new HashSet<string>(); // Use HashSet to automatically handle duplicates
        string? nextContinuationToken = null;

        var client = clientFactory.CreateClient(blobSettings.DefaultDocumentStore);
        var container = client.GetBlobContainerClient(blobSettings.DefaultDocumentContainerName);

        // Ensure container exists
        if (!await container.ExistsAsync(cancellationToken))
        {
            // Return empty result if container doesn't exist
            return new PagedResult<string>
            {
                Items = [],
                PageSize = pageSize,
                ContinuationToken = null
            };
        }

        // Use Azure Blob Storage's native pagination with continuation tokens
        var resultSegment = container
            .GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken)
            .AsPages(continuationToken, pageSize);

        // Get only the first page (we only need one page per call)
        await foreach (var page in resultSegment.WithCancellation(cancellationToken))
        {
            foreach (var blobItem in page.Values)
            {
                var objectId = ExtractObjectId(blobItem.Name, objectNameLower);
                if (!string.IsNullOrEmpty(objectId))
                {
                    items.Add(objectId); // HashSet prevents duplicates
                }
            }

            nextContinuationToken = page.ContinuationToken;
            break; // Only process first page
        }

        return new PagedResult<string>
        {
            Items = items.ToList(),
            PageSize = pageSize,
            ContinuationToken = nextContinuationToken
        };
    }

    /// <summary>
    /// Checks if an object document exists for the given ID.
    /// </summary>
    /// <param name="objectName">The object type name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the object exists, false otherwise.</returns>
    public async Task<bool> ExistsAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var objectNameLower = objectName.ToLowerInvariant();
        var blobPath = $"{objectNameLower}/{objectId}.json";

        var client = clientFactory.CreateClient(blobSettings.DefaultDocumentStore);
        var container = client.GetBlobContainerClient(blobSettings.DefaultDocumentContainerName);

        // Check if container exists first
        if (!await container.ExistsAsync(cancellationToken))
        {
            return false;
        }

        var blobClient = container.GetBlobClient(blobPath);
        return await blobClient.ExistsAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the total count of objects for the given type.
    /// Warning: This may be expensive for large datasets as it requires enumerating all items.
    /// </summary>
    /// <param name="objectName">The object type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total count of unique object IDs.</returns>
    public async Task<long> CountAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        var objectNameLower = objectName.ToLowerInvariant();
        var prefix = $"{objectNameLower}/";
        var objectIds = new HashSet<string>();

        var client = clientFactory.CreateClient(blobSettings.DefaultDocumentStore);
        var container = client.GetBlobContainerClient(blobSettings.DefaultDocumentContainerName);

        // Check if container exists first
        if (!await container.ExistsAsync(cancellationToken))
        {
            return 0;
        }

        await foreach (var blobItem in container.GetBlobsAsync(
            prefix: prefix,
            cancellationToken: cancellationToken))
        {
            var objectId = ExtractObjectId(blobItem.Name, objectNameLower);
            if (!string.IsNullOrEmpty(objectId))
            {
                objectIds.Add(objectId);
            }
        }

        return objectIds.Count;
    }

    /// <summary>
    /// Extracts the object ID from a blob path.
    /// </summary>
    /// <param name="blobPath">The full blob path (e.g., "project/12345-guid.json").</param>
    /// <param name="objectName">The object type name (already lowercased).</param>
    /// <returns>The extracted object ID, or empty string if extraction fails.</returns>
    private static string ExtractObjectId(string blobPath, string objectName)
    {
        // Expected format: {objectName}/{objectId}.json
        var prefix = $"{objectName}/";
        ReadOnlySpan<char> pathSpan = blobPath.AsSpan();

        if (!pathSpan.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var remainder = pathSpan[prefix.Length..];

        // Remove .json extension if present
        if (remainder.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            remainder = remainder[..^5]; // Remove ".json" (5 characters)
        }

        return remainder.ToString();
    }
}
