using System.Net;
using System.Text.Json;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Provides CosmosDB-backed implementation of <see cref="IObjectIdProvider"/>.
/// Uses continuation tokens for efficient pagination through large object collections.
/// </summary>
public class CosmosDbObjectIdProvider : IObjectIdProvider
{
    private sealed record CountResult(long Count);
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private Container? documentsContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbObjectIdProvider"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="settings">The CosmosDB settings.</param>
    public CosmosDbObjectIdProvider(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(settings);

        this.cosmosClient = cosmosClient;
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
        var items = new List<string>();
        string? nextContinuationToken = null;

        var container = await GetDocumentsContainerAsync();

        var query = new QueryDefinition("SELECT c.objectId FROM c WHERE c.objectName = @objectName")
            .WithParameter("@objectName", objectNameLower);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(objectNameLower),
            MaxItemCount = pageSize
        };

        try
        {
            using var iterator = container.GetItemQueryIterator<JsonElement>(
                query,
                continuationToken: continuationToken,
                requestOptions: queryOptions);

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                foreach (var item in response)
                {
                    items.Add(item.GetProperty("objectId").GetString()!);
                }
                nextContinuationToken = response.ContinuationToken;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new PagedResult<string>
            {
                Items = [],
                PageSize = pageSize,
                ContinuationToken = null
            };
        }

        return new PagedResult<string>
        {
            Items = items,
            PageSize = pageSize,
            ContinuationToken = nextContinuationToken
        };
    }

    /// <summary>
    /// Checks if an object document exists for the given ID.
    /// Uses point read for optimal RU efficiency (1 RU).
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
        var documentId = CosmosDbDocumentEntity.CreateId(objectName, objectId);

        var container = await GetDocumentsContainerAsync();

        try
        {
            await container.ReadItemAsync<dynamic>(
                documentId,
                new PartitionKey(objectNameLower),
                new ItemRequestOptions { EnableContentResponseOnWrite = false },
                cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the total count of objects for the given type.
    /// Uses aggregation query for efficient counting.
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
        var container = await GetDocumentsContainerAsync();

        var query = new QueryDefinition("SELECT COUNT(1) AS count FROM c WHERE c.objectName = @objectName")
            .WithParameter("@objectName", objectNameLower);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(objectNameLower)
        };

        try
        {
            using var iterator = container.GetItemQueryIterator<CountResult>(query, requestOptions: queryOptions);
            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                return response.FirstOrDefault()?.Count ?? 0;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return 0;
        }

        return 0;
    }

    private async Task<Container> GetDocumentsContainerAsync()
    {
        if (documentsContainer != null)
        {
            return documentsContainer;
        }

        Database database;
        if (settings.AutoCreateContainers)
        {
            // Create database if it doesn't exist
            var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(settings.DatabaseName);
            database = databaseResponse.Database;

            var containerProperties = new ContainerProperties(settings.DocumentsContainerName, "/objectName");
            await database.CreateContainerIfNotExistsAsync(containerProperties);
        }
        else
        {
            database = cosmosClient.GetDatabase(settings.DatabaseName);
        }

        documentsContainer = database.GetContainer(settings.DocumentsContainerName);
        return documentsContainer;
    }
}
