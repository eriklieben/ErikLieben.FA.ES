using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Extensions;
using System.Net;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Provides an S3-backed implementation of <see cref="IObjectIdProvider"/>.
/// Uses continuation tokens for efficient pagination through large object collections.
/// </summary>
public class S3ObjectIdProvider : IObjectIdProvider
{
    private readonly IS3ClientFactory clientFactory;
    private readonly EventStreamS3Settings settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ObjectIdProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory used to create <see cref="IAmazonS3"/> instances.</param>
    /// <param name="settings">The S3 storage settings used for bucket and key configuration.</param>
    public S3ObjectIdProvider(
        IS3ClientFactory clientFactory,
        EventStreamS3Settings settings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        this.clientFactory = clientFactory;
        this.settings = settings;
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
        var items = new HashSet<string>();

        var client = clientFactory.CreateClient(settings.DefaultDocumentStore);
        var bucketName = settings.DefaultDocumentContainerName.ToLowerInvariant();

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = prefix,
            Delimiter = "/",
            MaxKeys = pageSize,
            ContinuationToken = continuationToken,
        };

        try
        {
            var response = await client.ListObjectsV2Async(request, cancellationToken);

            foreach (var s3Object in response.S3Objects)
            {
                var objectId = ExtractObjectId(s3Object.Key, objectNameLower);
                if (!string.IsNullOrEmpty(objectId))
                {
                    items.Add(objectId);
                }
            }

            return new PagedResult<string>
            {
                Items = items.ToList(),
                PageSize = pageSize,
                ContinuationToken = response.IsTruncated == true ? response.NextContinuationToken : null,
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchBucket")
        {
            return new PagedResult<string>
            {
                Items = [],
                PageSize = pageSize,
                ContinuationToken = null,
            };
        }
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
        var key = $"{objectNameLower}/{objectId}.json";
        var bucketName = settings.DefaultDocumentContainerName.ToLowerInvariant();

        var client = clientFactory.CreateClient(settings.DefaultDocumentStore);
        return await client.ObjectExistsAsync(bucketName, key);
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
        var bucketName = settings.DefaultDocumentContainerName.ToLowerInvariant();

        var client = clientFactory.CreateClient(settings.DefaultDocumentStore);
        string? continuationToken = null;

        try
        {
            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken,
                };

                var response = await client.ListObjectsV2Async(request, cancellationToken);

                foreach (var s3Object in response.S3Objects)
                {
                    var objectId = ExtractObjectId(s3Object.Key, objectNameLower);
                    if (!string.IsNullOrEmpty(objectId))
                    {
                        objectIds.Add(objectId);
                    }
                }

                continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            }
            while (continuationToken != null);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchBucket")
        {
            return 0;
        }

        return objectIds.Count;
    }

    /// <summary>
    /// Extracts the object ID from an S3 object key.
    /// </summary>
    /// <param name="objectKey">The full S3 object key (e.g., "project/12345-guid.json").</param>
    /// <param name="objectName">The object type name (already lowercased).</param>
    /// <returns>The extracted object ID, or empty string if extraction fails.</returns>
    private static string ExtractObjectId(string objectKey, string objectName)
    {
        // Expected format: {objectName}/{objectId}.json
        var prefix = $"{objectName}/";
        ReadOnlySpan<char> keySpan = objectKey.AsSpan();

        if (!keySpan.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var remainder = keySpan[prefix.Length..];

        // Remove .json extension if present
        if (remainder.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            remainder = remainder[..^5]; // Remove ".json" (5 characters)
        }

        return remainder.ToString();
    }
}
