using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Exceptions;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Provides CosmosDB-backed persistence for object documents and their stream metadata.
/// Optimized for RU efficiency with proper partition key strategies.
/// </summary>
public class CosmosDbDocumentStore : ICosmosDbDocumentStore
{
    private readonly CosmosClient cosmosClient;
    private readonly EventStreamCosmosDbSettings settings;
    private readonly IDocumentTagDocumentFactory documentTagStoreFactory;
    private Container? documentsContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbDocumentStore"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="documentTagStoreFactory">The factory used to access document tag storage.</param>
    /// <param name="settings">The CosmosDB settings for containers and throughput.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public CosmosDbDocumentStore(
        CosmosClient cosmosClient,
        IDocumentTagDocumentFactory documentTagStoreFactory,
        EventStreamCosmosDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(documentTagStoreFactory);
        ArgumentNullException.ThrowIfNull(settings);

        this.cosmosClient = cosmosClient;
        this.documentTagStoreFactory = documentTagStoreFactory;
        this.settings = settings;
    }

    /// <summary>
    /// Creates a new document entity with initialized stream metadata if missing; returns the materialized document.
    /// </summary>
    /// <param name="name">The object name used as partition key.</param>
    /// <param name="objectId">The identifier of the object to create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The created or existing object document loaded from storage.</returns>
    [return: MaybeNull]
    public async Task<IObjectDocument> CreateAsync(
        string name,
        string objectId,
        string? store = null)
    {
        var container = await GetDocumentsContainerAsync();
        var partitionKey = new PartitionKey(name.ToLowerInvariant());
        var documentId = CosmosDbDocumentEntity.CreateId(name, objectId);

        try
        {
            // Try to read existing document first (point read = 1 RU)
            var response = await container.ReadItemAsync<CosmosDbDocumentEntity>(
                documentId,
                partitionKey);

            return ToObjectDocument(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Document doesn't exist, create it
            var targetStore = store ?? settings.DefaultDataStore;
            var nameLower = name.ToLowerInvariant();
            var newEntity = new CosmosDbDocumentEntity
            {
                Id = documentId,
                ObjectName = nameLower,
                ObjectId = objectId,
                Active = new CosmosDbStreamInfo
                {
                    StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
                    StreamType = CosmosDbStreamInfo.DefaultStoreType, // Always use cosmosdb for CosmosDB-created documents
                    DocumentTagType = CosmosDbStreamInfo.DefaultStoreType,
                    CurrentStreamVersion = -1,
                    DocumentType = CosmosDbStreamInfo.DefaultStoreType, // Route to CosmosDB in composite factories
                    EventStreamTagType = CosmosDbStreamInfo.DefaultStoreType,
                    DataStore = targetStore,
                    DocumentStore = targetStore,
                    DocumentTagStore = settings.DefaultDocumentTagStore,
                    StreamTagStore = settings.DefaultDocumentTagStore,
                    SnapShotStore = settings.DefaultSnapShotStore
                },
                SchemaVersion = "1.0.0"
            };

            var createResponse = await container.CreateItemAsync(newEntity, partitionKey);
            return ToObjectDocument(createResponse.Resource);
        }
    }

    /// <summary>
    /// Retrieves and materializes the object document from CosmosDB.
    /// </summary>
    /// <param name="name">The object name used as partition key.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetAsync(
        string name,
        string objectId,
        string? store = null)
    {
        var container = await GetDocumentsContainerAsync();
        var partitionKey = new PartitionKey(name.ToLowerInvariant());
        var documentId = CosmosDbDocumentEntity.CreateId(name, objectId);

        try
        {
            // Point read = 1 RU (most efficient)
            var response = await container.ReadItemAsync<CosmosDbDocumentEntity>(
                documentId,
                partitionKey);

            return ToObjectDocument(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new CosmosDbDocumentNotFoundException(
                $"The object document for object '{name}' with id '{objectId}' was not found.", ex);
        }
    }

    /// <summary>
    /// Retrieves the first document matching the given document tag.
    /// </summary>
    public async Task<IObjectDocument?> GetFirstByDocumentByTagAsync(
        string objectName,
        string tag,
        string? documentTagStore = null,
        string? store = null)
    {
        var targetDocumentTagStore = documentTagStore ?? settings.DefaultDocumentTagStore;
        var documentTagStoreInstance = documentTagStoreFactory.CreateDocumentTagStore(targetDocumentTagStore);
        var objectId = (await documentTagStoreInstance.GetAsync(objectName, tag)).FirstOrDefault();

        if (!string.IsNullOrEmpty(objectId))
        {
            return await GetAsync(objectName, objectId, store);
        }

        return null;
    }

    /// <summary>
    /// Retrieves all documents matching the given document tag.
    /// </summary>
    public async Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(
        string objectName,
        string tag,
        string? documentTagStore = null,
        string? store = null)
    {
        var targetDocumentTagStore = documentTagStore ?? settings.DefaultDocumentTagStore;
        var documentTagStoreInstance = documentTagStoreFactory.CreateDocumentTagStore(targetDocumentTagStore);
        var objectIds = await documentTagStoreInstance.GetAsync(objectName, tag);
        var documents = new List<IObjectDocument>();

        foreach (var objectId in objectIds)
        {
            documents.Add(await GetAsync(objectName, objectId, store));
        }

        return documents;
    }

    /// <summary>
    /// Persists the provided object document to CosmosDB, updating its hash for optimistic concurrency.
    /// </summary>
    /// <param name="document">The document to persist.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SetAsync(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var container = await GetDocumentsContainerAsync();
        var partitionKey = new PartitionKey(document.ObjectName.ToLowerInvariant());
        var documentId = CosmosDbDocumentEntity.CreateId(document.ObjectName, document.ObjectId);

        // Compute hash from active stream data
        var hashInput = string.Join("|",
            document.Active.StreamIdentifier,
            document.Active.StreamType,
            document.Active.CurrentStreamVersion,
            document.Active.DataStore,
            document.Active.DocumentStore);
        var hash = ComputeSha256Hash(hashInput);

        var entity = new CosmosDbDocumentEntity
        {
            Id = documentId,
            ObjectName = document.ObjectName,
            ObjectId = document.ObjectId,
            Active = new CosmosDbStreamInfo
            {
                StreamIdentifier = document.Active.StreamIdentifier,
                CurrentStreamVersion = document.Active.CurrentStreamVersion,
                StreamType = document.Active.StreamType,
                DocumentType = document.Active.DocumentType,
                DocumentTagType = document.Active.DocumentTagType,
                EventStreamTagType = document.Active.EventStreamTagType,
                DataStore = document.Active.DataStore,
                DocumentStore = document.Active.DocumentStore,
                DocumentTagStore = document.Active.DocumentTagStore,
                StreamTagStore = document.Active.StreamTagStore,
                SnapShotStore = document.Active.SnapShotStore
            },
            TerminatedStreams = document.TerminatedStreams.Select(ts => new CosmosDbTerminatedStreamInfo
            {
                StreamIdentifier = ts.StreamIdentifier,
                FinalVersion = ts.StreamVersion,
                TerminatedAt = ts.TerminationDate,
                Reason = ts.Reason
            }).ToList(),
            SchemaVersion = document.SchemaVersion ?? "1.0.0",
            Hash = hash,
            PrevHash = document.Hash
        };

        try
        {
            if (settings.UseOptimisticConcurrency && !string.IsNullOrEmpty(document.Hash))
            {
                // Read current ETag for optimistic concurrency
                try
                {
                    var existingResponse = await container.ReadItemAsync<CosmosDbDocumentEntity>(
                        documentId,
                        partitionKey);

                    var options = new ItemRequestOptions
                    {
                        IfMatchEtag = existingResponse.ETag
                    };
                    entity.ETag = existingResponse.Resource.ETag;
                    await container.ReplaceItemAsync(entity, documentId, partitionKey, options);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Document doesn't exist yet, create it
                    await container.CreateItemAsync(entity, partitionKey);
                }
            }
            else
            {
                // Try to replace, fallback to create if not found
                try
                {
                    await container.ReplaceItemAsync(entity, documentId, partitionKey);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Document doesn't exist yet, create it
                    await container.CreateItemAsync(entity, partitionKey);
                }
            }

            document.SetHash(hash, document.Hash);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new CosmosDbProcessingException(
                $"Optimistic concurrency conflict when updating document '{document.ObjectName}/{document.ObjectId}'.", ex);
        }
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
            var throughput = GetThroughputProperties(settings.DocumentsThroughput);
            var containerProperties = new ContainerProperties(settings.DocumentsContainerName, "/objectName");

            if (throughput != null)
            {
                await database.CreateContainerIfNotExistsAsync(containerProperties, throughput);
            }
            else
            {
                await database.CreateContainerIfNotExistsAsync(containerProperties);
            }
        }

        documentsContainer = database.GetContainer(settings.DocumentsContainerName);
        return documentsContainer;
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

    private static CosmosDbEventStreamDocument ToObjectDocument(CosmosDbDocumentEntity entity)
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = entity.Active.StreamIdentifier,
            StreamType = entity.Active.StreamType,
            DocumentTagType = entity.Active.DocumentTagType,
            CurrentStreamVersion = entity.Active.CurrentStreamVersion,
            DocumentType = entity.Active.DocumentType,
            EventStreamTagType = entity.Active.EventStreamTagType,
            DataStore = entity.Active.DataStore,
            DocumentStore = entity.Active.DocumentStore,
            DocumentTagStore = entity.Active.DocumentTagStore,
            StreamTagStore = entity.Active.StreamTagStore,
            SnapShotStore = entity.Active.SnapShotStore
        };

        var terminatedStreams = entity.TerminatedStreams.Select(ts => new TerminatedStream
        {
            StreamIdentifier = ts.StreamIdentifier,
            StreamVersion = ts.FinalVersion,
            TerminationDate = ts.TerminatedAt,
            Reason = ts.Reason
        });

        return new CosmosDbEventStreamDocument(
            entity.ObjectId,
            entity.ObjectName,
            streamInfo,
            terminatedStreams,
            entity.SchemaVersion,
            entity.Hash,
            entity.PrevHash);
    }

    private static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        Span<char> chars = stackalloc char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i].TryFormat(chars.Slice(i * 2, 2), out _, "x2");
        }
        return new string(chars);
    }
}
