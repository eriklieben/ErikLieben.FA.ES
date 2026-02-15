using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Retention;
using ErikLieben.FA.ES.S3.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// S3-compatible storage implementation of <see cref="IStreamMetadataProvider"/>.
/// Reads S3 object metadata to determine event count and date ranges for retention evaluation.
/// </summary>
public class S3StreamMetadataProvider : IStreamMetadataProvider
{
    private readonly IS3ClientFactory _clientFactory;
    private readonly EventStreamS3Settings _settings;
    private readonly ILogger<S3StreamMetadataProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3StreamMetadataProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory for creating client instances.</param>
    /// <param name="settings">The S3 storage settings.</param>
    /// <param name="logger">Optional logger.</param>
    public S3StreamMetadataProvider(
        IS3ClientFactory clientFactory,
        EventStreamS3Settings settings,
        ILogger<S3StreamMetadataProvider>? logger = null)
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

        var s3Client = _clientFactory.CreateClient(_settings.DefaultDocumentStore);
        var bucketName = _settings.DefaultDocumentContainerName;

        try
        {
            var objectNameLower = objectName.ToLowerInvariant();
            var prefix = $"{objectNameLower}/{objectId}";
            var eventCount = 0;
            DateTimeOffset? oldest = null;
            DateTimeOffset? newest = null;

            var request = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = prefix,
            };

            ListObjectsV2Response response;
            do
            {
                response = await s3Client.ListObjectsV2Async(request, cancellationToken);

                foreach (var s3Object in response.S3Objects)
                {
                    eventCount++;

                    var lastModified = (DateTimeOffset)(s3Object.LastModified ?? DateTime.UtcNow);
                    if (oldest == null || lastModified < oldest)
                    {
                        oldest = lastModified;
                    }

                    if (newest == null || lastModified > newest)
                    {
                        newest = lastModified;
                    }
                }

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);

            if (eventCount == 0)
            {
                return null;
            }

            return new StreamMetadata(objectName, objectId, eventCount, oldest, newest);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger?.LogDebug(ex, "Bucket or prefix not found for {ObjectName}/{ObjectId}", objectName, objectId);
            return null;
        }
    }
}
