using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Extensions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// S3-compatible storage backed implementation of <see cref="IProjectionStatusCoordinator"/>.
/// Persists projection status as JSON documents under a configurable key prefix.
/// Uses S3 ETags for optimistic concurrency control when the provider supports conditional writes.
/// </summary>
public class S3ProjectionStatusCoordinator : IProjectionStatusCoordinator
{
    private readonly IS3ClientFactory _clientFactory;
    private readonly EventStreamS3Settings _settings;
    private readonly string _prefix;
    private readonly ILogger<S3ProjectionStatusCoordinator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ProjectionStatusCoordinator"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory.</param>
    /// <param name="settings">The S3 storage settings.</param>
    /// <param name="prefix">The key prefix for storing projection status documents. Defaults to "projection-status".</param>
    /// <param name="logger">Optional logger.</param>
    public S3ProjectionStatusCoordinator(
        IS3ClientFactory clientFactory,
        EventStreamS3Settings settings,
        string prefix = "projection-status",
        ILogger<S3ProjectionStatusCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(prefix);

        _clientFactory = clientFactory;
        _settings = settings;
        _prefix = prefix;
        _logger = logger;
    }

    private string GetObjectKey(string projectionName, string objectId) =>
        $"{_prefix}/{projectionName}_{objectId}.json";

    /// <inheritdoc />
    public async Task<RebuildToken> StartRebuildAsync(
        string projectionName,
        string objectId,
        RebuildStrategy strategy,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectionName);
        ArgumentNullException.ThrowIfNull(objectId);

        var token = RebuildToken.Create(projectionName, objectId, strategy, timeout);
        var rebuildInfo = RebuildInfo.Start(strategy);
        var statusInfo = new ProjectionStatusInfo(
            projectionName,
            objectId,
            ProjectionStatus.Rebuilding,
            DateTimeOffset.UtcNow,
            0,
            rebuildInfo);

        var document = new StatusDocument(statusInfo, token);
        await UploadDocumentAsync(projectionName, objectId, document, etag: null, cancellationToken);

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(
                "Started rebuild for {ProjectionName}:{ObjectId} with strategy {Strategy}, expires at {ExpiresAt}",
                projectionName, objectId, strategy, token.ExpiresAt);
        }

        return token;
    }

    /// <inheritdoc />
    public async Task StartCatchUpAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var (document, etag) = await DownloadDocumentAsync(token.ProjectionName, token.ObjectId, cancellationToken);
        ValidateToken(token, document);

        if (document?.StatusInfo != null)
        {
            var updated = document.StatusInfo with
            {
                Status = ProjectionStatus.CatchingUp,
                StatusChangedAt = DateTimeOffset.UtcNow,
                RebuildInfo = document.StatusInfo.RebuildInfo?.WithProgress()
            };

            var updatedDocument = new StatusDocument(updated, document.ActiveRebuildToken);
            await UploadDocumentAsync(token.ProjectionName, token.ObjectId, updatedDocument, etag, cancellationToken);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Started catch-up for {ProjectionName}:{ObjectId}",
                    token.ProjectionName, token.ObjectId);
            }
        }
    }

    /// <inheritdoc />
    public async Task MarkReadyAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var (document, etag) = await DownloadDocumentAsync(token.ProjectionName, token.ObjectId, cancellationToken);
        ValidateToken(token, document);

        if (document?.StatusInfo != null)
        {
            var updated = document.StatusInfo with
            {
                Status = ProjectionStatus.Ready,
                StatusChangedAt = DateTimeOffset.UtcNow,
                RebuildInfo = document.StatusInfo.RebuildInfo?.WithCompletion()
            };

            var updatedDocument = new StatusDocument(updated, document.ActiveRebuildToken);
            await UploadDocumentAsync(token.ProjectionName, token.ObjectId, updatedDocument, etag, cancellationToken);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Marked {ProjectionName}:{ObjectId} as ready",
                    token.ProjectionName, token.ObjectId);
            }
        }
    }

    /// <inheritdoc />
    public async Task CompleteRebuildAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var (document, etag) = await DownloadDocumentAsync(token.ProjectionName, token.ObjectId, cancellationToken);
        ValidateToken(token, document);

        if (document?.StatusInfo != null)
        {
            var updated = document.StatusInfo with
            {
                Status = ProjectionStatus.Active,
                StatusChangedAt = DateTimeOffset.UtcNow,
                RebuildInfo = document.StatusInfo.RebuildInfo?.WithCompletion()
            };

            // Clear the active rebuild token on completion
            var updatedDocument = new StatusDocument(updated, null);
            await UploadDocumentAsync(token.ProjectionName, token.ObjectId, updatedDocument, etag, cancellationToken);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Completed rebuild for {ProjectionName}:{ObjectId}",
                    token.ProjectionName, token.ObjectId);
            }
        }
    }

    /// <inheritdoc />
    public async Task CancelRebuildAsync(
        RebuildToken token,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var (document, etag) = await DownloadDocumentAsync(token.ProjectionName, token.ObjectId, cancellationToken);

        if (document?.StatusInfo != null)
        {
            var newStatus = error != null ? ProjectionStatus.Failed : ProjectionStatus.Active;
            var rebuildInfo = error != null
                ? document.StatusInfo.RebuildInfo?.WithError(error)
                : document.StatusInfo.RebuildInfo?.WithCompletion();

            var updated = document.StatusInfo with
            {
                Status = newStatus,
                StatusChangedAt = DateTimeOffset.UtcNow,
                RebuildInfo = rebuildInfo
            };

            // Clear the active rebuild token on cancel
            var updatedDocument = new StatusDocument(updated, null);
            await UploadDocumentAsync(token.ProjectionName, token.ObjectId, updatedDocument, etag, cancellationToken);
        }

        if (_logger?.IsEnabled(LogLevel.Warning) == true)
        {
            _logger.LogWarning(
                "Cancelled rebuild for {ProjectionName}:{ObjectId}. Error: {Error}",
                token.ProjectionName, token.ObjectId, error ?? "none");
        }
    }

    /// <inheritdoc />
    public async Task<ProjectionStatusInfo?> GetStatusAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var (document, _) = await DownloadDocumentAsync(projectionName, objectId, cancellationToken);
        return document?.StatusInfo;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProjectionStatusInfo>> GetByStatusAsync(
        ProjectionStatus status,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProjectionStatusInfo>();
        var s3Client = _clientFactory.CreateClient(_settings.DefaultDataStore);
        var bucketName = _settings.BucketName;

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = _prefix + "/"
        };

        ListObjectsV2Response response;
        do
        {
            response = await s3Client.ListObjectsV2Async(request, cancellationToken);

            foreach (var obj in response.S3Objects)
            {
                if (!obj.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var document = await DownloadDocumentFromKeyAsync(s3Client, bucketName, obj.Key, cancellationToken);

                if (document?.StatusInfo != null && document.StatusInfo.Status == status)
                {
                    results.Add(document.StatusInfo);
                }
            }

            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);

        return results;
    }

    /// <inheritdoc />
    public async Task<int> RecoverStuckRebuildsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var recovered = 0;
        var s3Client = _clientFactory.CreateClient(_settings.DefaultDataStore);
        var bucketName = _settings.BucketName;

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = _prefix + "/"
        };

        ListObjectsV2Response response;
        do
        {
            response = await s3Client.ListObjectsV2Async(request, cancellationToken);

            foreach (var obj in response.S3Objects)
            {
                if (!obj.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var (document, etag) = await DownloadDocumentWithETagFromKeyAsync(s3Client, bucketName, obj.Key, cancellationToken);

                if (document?.ActiveRebuildToken != null &&
                    document.ActiveRebuildToken.IsExpired &&
                    document.StatusInfo != null &&
                    document.StatusInfo.Status.IsRebuilding())
                {
                    var updated = document.StatusInfo with
                    {
                        Status = ProjectionStatus.Failed,
                        StatusChangedAt = now,
                        RebuildInfo = document.StatusInfo.RebuildInfo?.WithError("Rebuild timed out")
                    };

                    var updatedDocument = new StatusDocument(updated, null);
                    await UploadDocumentToKeyAsync(s3Client, bucketName, obj.Key, updatedDocument, etag, cancellationToken);
                    recovered++;

                    if (_logger?.IsEnabled(LogLevel.Warning) == true)
                    {
                        _logger.LogWarning(
                            "Recovered stuck rebuild for {ProjectionName}:{ObjectId}",
                            document.ActiveRebuildToken.ProjectionName, document.ActiveRebuildToken.ObjectId);
                    }
                }
            }

            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);

        return recovered;
    }

    /// <inheritdoc />
    public async Task DisableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var (document, etag) = await DownloadDocumentAsync(projectionName, objectId, cancellationToken);

        ProjectionStatusInfo statusInfo;
        if (document?.StatusInfo != null)
        {
            statusInfo = document.StatusInfo with
            {
                Status = ProjectionStatus.Disabled,
                StatusChangedAt = DateTimeOffset.UtcNow
            };
        }
        else
        {
            statusInfo = new ProjectionStatusInfo(
                projectionName,
                objectId,
                ProjectionStatus.Disabled,
                DateTimeOffset.UtcNow,
                0);
        }

        var updatedDocument = new StatusDocument(statusInfo, document?.ActiveRebuildToken);
        await UploadDocumentAsync(projectionName, objectId, updatedDocument, etag, cancellationToken);

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(
                "Disabled projection {ProjectionName}:{ObjectId}",
                projectionName, objectId);
        }
    }

    /// <inheritdoc />
    public async Task EnableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var (document, etag) = await DownloadDocumentAsync(projectionName, objectId, cancellationToken);

        if (document?.StatusInfo != null)
        {
            var updated = document.StatusInfo with
            {
                Status = ProjectionStatus.Active,
                StatusChangedAt = DateTimeOffset.UtcNow
            };

            var updatedDocument = new StatusDocument(updated, document.ActiveRebuildToken);
            await UploadDocumentAsync(projectionName, objectId, updatedDocument, etag, cancellationToken);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Enabled projection {ProjectionName}:{ObjectId}",
                    projectionName, objectId);
            }
        }
    }

    private static void ValidateToken(RebuildToken token, StatusDocument? document)
    {
        if (document?.ActiveRebuildToken == null ||
            document.ActiveRebuildToken.Token != token.Token)
        {
            throw new InvalidOperationException(
                $"Invalid or expired rebuild token for {token.ProjectionName}:{token.ObjectId}");
        }

        if (token.IsExpired)
        {
            throw new InvalidOperationException(
                $"Rebuild token for {token.ProjectionName}:{token.ObjectId} has expired");
        }
    }

    private async Task<(StatusDocument?, string?)> DownloadDocumentAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken)
    {
        var key = GetObjectKey(projectionName, objectId);
        var s3Client = _clientFactory.CreateClient(_settings.DefaultDataStore);
        var bucketName = _settings.BucketName;
        return await DownloadDocumentWithETagFromKeyAsync(s3Client, bucketName, key, cancellationToken);
    }

    private static async Task<(StatusDocument?, string?)> DownloadDocumentWithETagFromKeyAsync(
        IAmazonS3 s3Client,
        string bucketName,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            using var response = await s3Client.GetObjectAsync(request, cancellationToken);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            var json = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            var document = JsonSerializer.Deserialize(
                json,
                S3StatusDocumentJsonContext.Default.StatusDocument);
            return (document, response.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey" || ex.ErrorCode == "NoSuchBucket")
        {
            return (null, null);
        }
    }

    private static async Task<StatusDocument?> DownloadDocumentFromKeyAsync(
        IAmazonS3 s3Client,
        string bucketName,
        string key,
        CancellationToken cancellationToken)
    {
        var (document, _) = await DownloadDocumentWithETagFromKeyAsync(s3Client, bucketName, key, cancellationToken);
        return document;
    }

    private async Task UploadDocumentAsync(
        string projectionName,
        string objectId,
        StatusDocument document,
        string? etag,
        CancellationToken cancellationToken)
    {
        var key = GetObjectKey(projectionName, objectId);
        var s3Client = _clientFactory.CreateClient(_settings.DefaultDataStore);
        var bucketName = _settings.BucketName;
        await UploadDocumentToKeyAsync(s3Client, bucketName, key, document, etag, cancellationToken);
    }

    private async Task UploadDocumentToKeyAsync(
        IAmazonS3 s3Client,
        string bucketName,
        string key,
        StatusDocument document,
        string? etag,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(document, S3StatusDocumentJsonContext.Default.StatusDocument);
        var bytes = Encoding.UTF8.GetBytes(json);

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            ContentType = "application/json",
            InputStream = new MemoryStream(bytes)
        };

        if (_settings.SupportsConditionalWrites && !string.IsNullOrEmpty(etag))
        {
            request.Headers["If-Match"] = etag;
        }

        try
        {
            await s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new InvalidOperationException(
                $"Conditional write failed for s3://{bucketName}/{key}. " +
                $"The object was modified since it was last read (If-Match ETag: {etag}).", ex);
        }
    }

    /// <summary>
    /// Document stored in S3 containing both the projection status info and the active rebuild token.
    /// </summary>
    /// <param name="StatusInfo">The projection status information.</param>
    /// <param name="ActiveRebuildToken">The active rebuild token, or null if no rebuild is in progress.</param>
    internal record StatusDocument(
        ProjectionStatusInfo? StatusInfo,
        RebuildToken? ActiveRebuildToken);
}

/// <summary>
/// AOT-compatible JSON serializer context for <see cref="S3ProjectionStatusCoordinator"/>.
/// </summary>
[JsonSerializable(typeof(S3ProjectionStatusCoordinator.StatusDocument))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class S3StatusDocumentJsonContext : JsonSerializerContext
{
}
