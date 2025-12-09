using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Provides a CosmosDB-backed implementation of <see cref="IDocumentTagStore"/> for associating and querying document tags.
/// Uses partition key strategy based on object name and tag for efficient lookups.
/// </summary>
public partial class CosmosDbDocumentTagStore : IDocumentTagStore
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private Container? tagsContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbDocumentTagStore"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="settings">The CosmosDB settings.</param>
    public CosmosDbDocumentTagStore(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(settings);

        this.cosmosClient = cosmosClient;
        this.settings = settings;
    }

    /// <summary>
    /// Associates the specified tag with the given document by storing a tag entity in CosmosDB.
    /// </summary>
    /// <param name="document">The document to tag.</param>
    /// <param name="tag">The tag value to associate with the document.</param>
    /// <returns>A task that represents the asynchronous tagging operation.</returns>
    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);

        var container = await GetTagsContainerAsync();

        var sanitizedTag = SanitizeTag(tag);
        var tagKey = CosmosDbTagEntity.CreateTagKey(document.ObjectName, sanitizedTag);

        var entity = new CosmosDbTagEntity
        {
            Id = CosmosDbTagEntity.CreateId("document", document.ObjectName, sanitizedTag, document.ObjectId),
            TagKey = tagKey,
            TagType = "document",
            ObjectName = document.ObjectName,
            Tag = tag,
            ObjectId = document.ObjectId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var partitionKey = new PartitionKey(tagKey);

        // Upsert to handle both insert and update
        await container.UpsertItemAsync(entity, partitionKey);
    }

    /// <summary>
    /// Gets the identifiers of documents that have the specified tag within the given object scope.
    /// Uses partition key query for optimal RU efficiency.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of document identifiers; empty when no documents have the tag.</returns>
    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        var container = await GetTagsContainerAsync();

        var sanitizedTag = SanitizeTag(tag);
        var tagKey = CosmosDbTagEntity.CreateTagKey(objectName, sanitizedTag);
        var partitionKey = new PartitionKey(tagKey);

        var query = new QueryDefinition(
            "SELECT c.objectId FROM c WHERE c.tagKey = @tagKey AND c.tagType = 'document'")
            .WithParameter("@tagKey", tagKey);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = partitionKey
        };

        var objectIds = new List<string>();

        try
        {
            using var iterator = container.GetItemQueryIterator<JsonElement>(query, requestOptions: queryOptions);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    objectIds.Add(item.GetProperty("objectId").GetString()!);
                }
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        return objectIds;
    }

    private async Task<Container> GetTagsContainerAsync()
    {
        if (tagsContainer != null)
        {
            return tagsContainer;
        }

        Database database;
        if (settings.AutoCreateContainers)
        {
            // Create database if it doesn't exist
            var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                settings.DatabaseName,
                settings.DatabaseThroughput != null
                    ? GetThroughputProperties(settings.DatabaseThroughput)
                    : null);
            database = databaseResponse.Database;
        }
        else
        {
            database = cosmosClient.GetDatabase(settings.DatabaseName);
        }

        if (settings.AutoCreateContainers)
        {
            var throughput = GetThroughputProperties(settings.TagsThroughput);
            var containerProperties = new ContainerProperties(settings.TagsContainerName, "/tagKey");

            if (throughput != null)
            {
                await database.CreateContainerIfNotExistsAsync(containerProperties, throughput);
            }
            else
            {
                await database.CreateContainerIfNotExistsAsync(containerProperties);
            }
        }

        tagsContainer = database.GetContainer(settings.TagsContainerName);
        return tagsContainer;
    }

    private static ThroughputProperties? GetThroughputProperties(ThroughputSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        if (settings.AutoscaleMaxThroughput.HasValue)
        {
            return ThroughputProperties.CreateAutoscaleThroughput(settings.AutoscaleMaxThroughput.Value);
        }

        if (settings.ManualThroughput.HasValue)
        {
            return ThroughputProperties.CreateManualThroughput(settings.ManualThroughput.Value);
        }

        return null;
    }

    /// <summary>
    /// Sanitizes a tag for use in IDs and keys.
    /// </summary>
    private static string SanitizeTag(string input)
    {
        return InvalidCharsRegex().Replace(input.ToLowerInvariant(), string.Empty);
    }

    [GeneratedRegex(@"[/\\#?\u0000-\u001F\u007F-\u009F]")]
    private static partial Regex InvalidCharsRegex();
}
