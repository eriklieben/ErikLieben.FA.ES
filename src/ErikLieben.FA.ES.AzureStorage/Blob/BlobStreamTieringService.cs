using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Implementation of <see cref="IBlobStreamTieringService"/>.
/// </summary>
public class BlobStreamTieringService : IBlobStreamTieringService
{
    private readonly IAzureClientFactory<BlobServiceClient> _clientFactory;
    private readonly EventStreamBlobSettings _settings;
    private readonly ILogger<BlobStreamTieringService>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStreamTieringService"/> class.
    /// </summary>
    /// <param name="clientFactory">The blob client factory.</param>
    /// <param name="settings">The blob settings.</param>
    /// <param name="logger">Optional logger.</param>
    public BlobStreamTieringService(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        EventStreamBlobSettings settings,
        ILogger<BlobStreamTieringService>? logger = null)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TieringResult> SetStreamTierAsync(
        string objectName,
        string streamId,
        AccessTier tier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectName);
        ArgumentNullException.ThrowIfNull(streamId);

        try
        {
            var container = await GetContainerAsync(objectName);
            var prefix = GetStreamPrefix(streamId);
            AccessTier? previousTier = null;
            var count = 0;

            await foreach (var blob in container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                if (count == 0 && blob.Properties.AccessTier.HasValue)
                {
                    previousTier = blob.Properties.AccessTier.Value;
                }

                var blobClient = container.GetBlobClient(blob.Name);
                await blobClient.SetAccessTierAsync(tier, cancellationToken: cancellationToken);
                count++;
            }

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Changed tier for {Count} blobs in stream {StreamId} from {PreviousTier} to {NewTier}",
                    count, streamId, previousTier, tier);
            }

            return TieringResult.Succeeded(streamId, previousTier, tier, count);
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(ex, "Failed to set tier for stream {StreamId}", streamId);
            }
            return TieringResult.Failed(streamId, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<AccessTier?> GetStreamTierAsync(
        string objectName,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectName);
        ArgumentNullException.ThrowIfNull(streamId);

        var container = await GetContainerAsync(objectName);
        var prefix = GetStreamPrefix(streamId);

        var enumerator = container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            if (await enumerator.MoveNextAsync())
            {
                return enumerator.Current.Properties.AccessTier;
            }
            return null;
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async Task<RehydrationResult> RehydrateStreamAsync(
        string objectName,
        string streamId,
        RehydratePriority priority = RehydratePriority.Standard,
        AccessTier? targetTier = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objectName);
        ArgumentNullException.ThrowIfNull(streamId);

        try
        {
            var container = await GetContainerAsync(objectName);
            var prefix = GetStreamPrefix(streamId);
            var rehydrateToTier = targetTier ?? AccessTier.Hot;
            var rehydratePriorityValue = priority == RehydratePriority.High
                ? Azure.Storage.Blobs.Models.RehydratePriority.High
                : Azure.Storage.Blobs.Models.RehydratePriority.Standard;

            var needsRehydration = false;

            await foreach (var blob in container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                if (blob.Properties.AccessTier == AccessTier.Archive)
                {
                    needsRehydration = true;
                    var blobClient = container.GetBlobClient(blob.Name);
                    await blobClient.SetAccessTierAsync(
                        rehydrateToTier,
                        rehydratePriority: rehydratePriorityValue,
                        cancellationToken: cancellationToken);
                }
            }

            if (!needsRehydration)
            {
                return RehydrationResult.NotNeeded(streamId);
            }

            var estimatedDuration = priority == RehydratePriority.High
                ? TimeSpan.FromHours(1)
                : TimeSpan.FromHours(15);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Initiated rehydration for stream {StreamId} with {Priority} priority, estimated {Duration}",
                    streamId, priority, estimatedDuration);
            }

            return RehydrationResult.Succeeded(streamId, estimatedDuration);
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(ex, "Failed to rehydrate stream {StreamId}", streamId);
            }
            return RehydrationResult.Failed(streamId, ex.Message);
        }
    }

    private async Task<BlobContainerClient> GetContainerAsync(string objectName)
    {
        var client = _clientFactory.CreateClient(_settings.DefaultDataStore);
        var container = client.GetBlobContainerClient(objectName.ToLowerInvariant());

        if (_settings.AutoCreateContainer)
        {
            await container.CreateIfNotExistsAsync();
        }

        return container;
    }

    private static string GetStreamPrefix(string streamId)
    {
        return $"eventstream/{streamId}";
    }
}
