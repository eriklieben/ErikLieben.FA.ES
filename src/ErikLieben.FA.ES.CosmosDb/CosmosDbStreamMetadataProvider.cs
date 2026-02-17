using System.Net;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Retention;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// CosmosDB implementation of <see cref="IStreamMetadataProvider"/>.
/// Queries the events container to determine event count and date ranges for retention evaluation.
/// </summary>
public class CosmosDbStreamMetadataProvider : IStreamMetadataProvider
{
    private readonly CosmosClient _cosmosClient;
    private readonly EventStreamCosmosDbSettings _settings;
    private readonly ILogger<CosmosDbStreamMetadataProvider>? _logger;
    private Container? _documentsContainer;
    private Container? _eventsContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbStreamMetadataProvider"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="settings">The CosmosDB settings.</param>
    /// <param name="logger">Optional logger.</param>
    public CosmosDbStreamMetadataProvider(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings settings,
        ILogger<CosmosDbStreamMetadataProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(settings);

        _cosmosClient = cosmosClient;
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
            var streamId = await GetStreamIdAsync(objectName, objectId, cancellationToken);
            if (streamId == null)
            {
                return null;
            }

            return await QueryEventMetadataAsync(objectName, objectId, streamId, cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug(
                    ex,
                    "Container or document not found for {ObjectName}/{ObjectId}",
                    objectName,
                    objectId);
            }

            return null;
        }
    }

    private async Task<string?> GetStreamIdAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken)
    {
        var container = GetDocumentsContainer();
        var documentId = CosmosDbDocumentEntity.CreateId(objectName, objectId);
        var partitionKey = new PartitionKey(objectName.ToLowerInvariant());

        try
        {
            var response = await container.ReadItemAsync<CosmosDbDocumentEntity>(
                documentId,
                partitionKey,
                cancellationToken: cancellationToken);

            return response.Resource?.Active?.StreamIdentifier;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<StreamMetadata?> QueryEventMetadataAsync(
        string objectName,
        string objectId,
        string streamId,
        CancellationToken cancellationToken)
    {
        var container = GetEventsContainer();

        var query = new QueryDefinition(
            "SELECT COUNT(1) AS eventCount, MIN(c.timestamp) AS oldest, MAX(c.timestamp) AS newest " +
            "FROM c WHERE c.streamId = @streamId AND c._type = 'event'")
            .WithParameter("@streamId", streamId);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(streamId)
        };

        using var iterator = container.GetItemQueryIterator<AggregateResult>(
            query,
            requestOptions: queryOptions);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            using var enumerator = response.GetEnumerator();
            if (enumerator.MoveNext())
            {
                var result = enumerator.Current;
                if (result.EventCount == 0)
                {
                    return null;
                }

                return new StreamMetadata(
                    objectName,
                    objectId,
                    result.EventCount,
                    result.Oldest,
                    result.Newest);
            }
        }

        return null;
    }

    private Container GetDocumentsContainer()
    {
        _documentsContainer ??= _cosmosClient
            .GetDatabase(_settings.DatabaseName)
            .GetContainer(_settings.DocumentsContainerName);

        return _documentsContainer;
    }

    private Container GetEventsContainer()
    {
        _eventsContainer ??= _cosmosClient
            .GetDatabase(_settings.DatabaseName)
            .GetContainer(_settings.EventsContainerName);

        return _eventsContainer;
    }

    /// <summary>
    /// Internal result type for the aggregate CosmosDB query.
    /// </summary>
    internal sealed class AggregateResult
    {
        /// <summary>
        /// The total number of events in the stream.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("eventCount")]
        public int EventCount { get; set; }

        /// <summary>
        /// The timestamp of the oldest event.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("oldest")]
        public DateTimeOffset? Oldest { get; set; }

        /// <summary>
        /// The timestamp of the newest event.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("newest")]
        public DateTimeOffset? Newest { get; set; }
    }
}
