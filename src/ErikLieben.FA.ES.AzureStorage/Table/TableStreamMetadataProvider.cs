using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using ErikLieben.FA.ES.Retention;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Azure Table Storage implementation of <see cref="IStreamMetadataProvider"/>.
/// Reads event entities to determine event count and date ranges for retention evaluation.
/// </summary>
public class TableStreamMetadataProvider : IStreamMetadataProvider
{
    private static readonly string[] SelectTimestamp = ["Timestamp", "PayloadChunkIndex"];
    private static readonly string[] SelectStreamIdentifier = ["ActiveStreamIdentifier"];

    private readonly IAzureClientFactory<TableServiceClient> _clientFactory;
    private readonly EventStreamTableSettings _settings;
    private readonly ILogger<TableStreamMetadataProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableStreamMetadataProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory for creating Table Service clients.</param>
    /// <param name="settings">The Table storage settings.</param>
    /// <param name="logger">Optional logger.</param>
    public TableStreamMetadataProvider(
        IAzureClientFactory<TableServiceClient> clientFactory,
        EventStreamTableSettings settings,
        ILogger<TableStreamMetadataProvider>? logger = null)
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

        try
        {
            var streamIdentifier = await GetStreamIdentifierAsync(objectName, objectId, cancellationToken);
            if (streamIdentifier == null)
            {
                return null;
            }

            return await CollectEventMetadataAsync(objectName, objectId, streamIdentifier, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug(ex, "Table or entity not found for {ObjectName}/{ObjectId}", objectName, objectId);
            }
            return null;
        }
    }

    private async Task<string?> GetStreamIdentifierAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken)
    {
        var serviceClient = _clientFactory.CreateClient(_settings.DefaultDocumentStore);
        var documentTableClient = serviceClient.GetTableClient(_settings.DefaultDocumentTableName);

        var partitionKey = objectName.ToLowerInvariant();

        try
        {
            var response = await documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(
                partitionKey,
                objectId,
                select: SelectStreamIdentifier,
                cancellationToken: cancellationToken);

            if (!response.HasValue || response.Value is null)
            {
                return null;
            }

            return response.Value.ActiveStreamIdentifier;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task<StreamMetadata?> CollectEventMetadataAsync(
        string objectName,
        string objectId,
        string streamIdentifier,
        CancellationToken cancellationToken)
    {
        var serviceClient = _clientFactory.CreateClient(_settings.DefaultDataStore);
        var eventTableClient = serviceClient.GetTableClient(_settings.DefaultEventTableName);

        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {streamIdentifier}");

        var eventCount = 0;
        DateTimeOffset? oldest = null;
        DateTimeOffset? newest = null;

#pragma warning disable S3267 // Loops should be simplified - await foreach cannot use LINQ without System.Linq.Async
        await foreach (var entity in eventTableClient.QueryAsync<TableEventEntity>(
            filter,
            select: SelectTimestamp,
            cancellationToken: cancellationToken))
        {
            // Skip payload chunk rows (they have a PayloadChunkIndex > 0)
            if (entity.PayloadChunkIndex.HasValue && entity.PayloadChunkIndex.Value > 0)
            {
                continue;
            }

            eventCount++;

            if (entity.Timestamp.HasValue)
            {
                var timestamp = entity.Timestamp.Value;
                if (oldest == null || timestamp < oldest)
                {
                    oldest = timestamp;
                }

                if (newest == null || timestamp > newest)
                {
                    newest = timestamp;
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
}
