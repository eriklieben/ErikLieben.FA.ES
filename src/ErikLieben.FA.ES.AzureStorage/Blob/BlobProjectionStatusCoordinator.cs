using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Azure Blob Storage backed implementation of <see cref="IProjectionStatusCoordinator"/>.
/// Persists projection status as JSON documents in a configurable container.
/// Uses blob ETags for optimistic concurrency control.
/// </summary>
public class BlobProjectionStatusCoordinator : IProjectionStatusCoordinator
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobProjectionStatusCoordinator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobProjectionStatusCoordinator"/> class.
    /// </summary>
    /// <param name="blobServiceClient">The Azure Blob Service client.</param>
    /// <param name="containerName">The container name for storing projection status documents. Defaults to "projection-status".</param>
    /// <param name="logger">Optional logger.</param>
    public BlobProjectionStatusCoordinator(
        BlobServiceClient blobServiceClient,
        string containerName = "projection-status",
        ILogger<BlobProjectionStatusCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);
        ArgumentNullException.ThrowIfNull(containerName);

        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        _logger = logger;
    }

    private static string GetBlobName(string projectionName, string objectId) =>
        $"{projectionName}_{objectId}.json";

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

        _logger?.LogInformation(
            "Started rebuild for {ProjectionName}:{ObjectId} with strategy {Strategy}, expires at {ExpiresAt}",
            projectionName, objectId, strategy, token.ExpiresAt);

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

            _logger?.LogInformation(
                "Started catch-up for {ProjectionName}:{ObjectId}",
                token.ProjectionName, token.ObjectId);
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

            _logger?.LogInformation(
                "Marked {ProjectionName}:{ObjectId} as ready",
                token.ProjectionName, token.ObjectId);
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

            _logger?.LogInformation(
                "Completed rebuild for {ProjectionName}:{ObjectId}",
                token.ProjectionName, token.ObjectId);
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

        _logger?.LogWarning(
            "Cancelled rebuild for {ProjectionName}:{ObjectId}. Error: {Error}",
            token.ProjectionName, token.ObjectId, error ?? "none");
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

        await foreach (var blobItem in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (!blobItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            var document = await DownloadDocumentFromBlobAsync(blobClient, cancellationToken);

            if (document?.StatusInfo != null && document.StatusInfo.Status == status)
            {
                results.Add(document.StatusInfo);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<int> RecoverStuckRebuildsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var recovered = 0;

        await foreach (var blobItem in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (!blobItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            var (document, etag) = await DownloadDocumentWithETagAsync(blobClient, cancellationToken);

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
                await UploadDocumentToBlobAsync(blobClient, updatedDocument, etag, cancellationToken);
                recovered++;

                _logger?.LogWarning(
                    "Recovered stuck rebuild for {ProjectionName}:{ObjectId}",
                    document.ActiveRebuildToken.ProjectionName, document.ActiveRebuildToken.ObjectId);
            }
        }

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

        _logger?.LogInformation(
            "Disabled projection {ProjectionName}:{ObjectId}",
            projectionName, objectId);
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

            _logger?.LogInformation(
                "Enabled projection {ProjectionName}:{ObjectId}",
                projectionName, objectId);
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

    private async Task<(StatusDocument?, ETag?)> DownloadDocumentAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken)
    {
        var blobName = GetBlobName(projectionName, objectId);
        var blobClient = _containerClient.GetBlobClient(blobName);
        return await DownloadDocumentWithETagAsync(blobClient, cancellationToken);
    }

    private static async Task<(StatusDocument?, ETag?)> DownloadDocumentWithETagAsync(
        BlobClient blobClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var content = response.Value.Content.ToString();
            var document = JsonSerializer.Deserialize(
                content,
                StatusDocumentJsonContext.Default.StatusDocument);
            var etag = response.Value.Details.ETag;
            return (document, etag);
        }
        catch (RequestFailedException ex)
            when (ex.Status == 404)
        {
            return (null, null);
        }
    }

    private static async Task<StatusDocument?> DownloadDocumentFromBlobAsync(
        BlobClient blobClient,
        CancellationToken cancellationToken)
    {
        var (document, _) = await DownloadDocumentWithETagAsync(blobClient, cancellationToken);
        return document;
    }

    private async Task UploadDocumentAsync(
        string projectionName,
        string objectId,
        StatusDocument document,
        ETag? etag,
        CancellationToken cancellationToken)
    {
        var blobName = GetBlobName(projectionName, objectId);
        var blobClient = _containerClient.GetBlobClient(blobName);
        await UploadDocumentToBlobAsync(blobClient, document, etag, cancellationToken);
    }

    private static async Task UploadDocumentToBlobAsync(
        BlobClient blobClient,
        StatusDocument document,
        ETag? etag,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(document, StatusDocumentJsonContext.Default.StatusDocument);
        var bytes = Encoding.UTF8.GetBytes(json);

        var conditions = etag.HasValue
            ? new BlobRequestConditions { IfMatch = etag.Value }
            : null;

        await blobClient.UploadAsync(
            new MemoryStream(bytes),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json",
                },
                Conditions = conditions
            },
            cancellationToken);
    }

    /// <summary>
    /// Document stored in blob storage containing both the projection status info and the active rebuild token.
    /// </summary>
    /// <param name="StatusInfo">The projection status information.</param>
    /// <param name="ActiveRebuildToken">The active rebuild token, or null if no rebuild is in progress.</param>
    internal record StatusDocument(
        ProjectionStatusInfo? StatusInfo,
        RebuildToken? ActiveRebuildToken);
}

/// <summary>
/// AOT-compatible JSON serializer context for <see cref="BlobProjectionStatusCoordinator"/>.
/// </summary>
[JsonSerializable(typeof(BlobProjectionStatusCoordinator.StatusDocument))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class StatusDocumentJsonContext : JsonSerializerContext
{
}
