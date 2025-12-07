using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using TableEventStreamDocument = ErikLieben.FA.ES.AzureStorage.Table.Model.TableEventStreamDocument;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Provides Azure Table Storage backed persistence for object documents and their stream metadata.
/// </summary>
public class TableDocumentStore : ITableDocumentStore
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly EventStreamTableSettings tableSettings;
    private readonly IDocumentTagDocumentFactory documentTagStoreFactory;
    private readonly EventStreamDefaultTypeSettings typeSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableDocumentStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
    /// <param name="documentTagStoreFactory">The factory used to access document tag storage.</param>
    /// <param name="tableSettings">The table storage settings used for tables and chunking.</param>
    /// <param name="typeSettings">The default type settings for streams, documents, and tags.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public TableDocumentStore(
        IAzureClientFactory<TableServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagStoreFactory,
        EventStreamTableSettings tableSettings,
        EventStreamDefaultTypeSettings typeSettings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(tableSettings);
        ArgumentNullException.ThrowIfNull(documentTagStoreFactory);
        ArgumentNullException.ThrowIfNull(typeSettings);

        this.clientFactory = clientFactory;
        this.tableSettings = tableSettings;
        this.documentTagStoreFactory = documentTagStoreFactory;
        this.typeSettings = typeSettings;
    }

    /// <summary>
    /// Creates a new document entity with initialized stream metadata if missing; returns the materialized document.
    /// </summary>
    /// <param name="name">The object name used to determine the partition key.</param>
    /// <param name="objectId">The identifier of the object to create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The created or existing object document loaded from storage.</returns>
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public async Task<IObjectDocument> CreateAsync(
        string name,
        string objectId,
        string? store = null)
    {
        var targetStore = store ?? tableSettings.DefaultDocumentStore;
        var documentTableClient = await GetTableClientAsync(targetStore, tableSettings.DefaultDocumentTableName);

        var partitionKey = name.ToLowerInvariant();
        var rowKey = objectId;

        try
        {
            var response = await documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(partitionKey, rowKey);
            if (!response.HasValue)
            {
                var newEntity = new TableDocumentEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    ObjectId = objectId,
                    ObjectName = name,

                    // Individual Active stream columns
                    ActiveStreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
                    ActiveStreamType = typeSettings.StreamType,
                    ActiveDocumentTagType = typeSettings.DocumentTagType,
                    ActiveCurrentStreamVersion = -1,
                    ActiveDocumentType = typeSettings.DocumentType,
                    ActiveEventStreamTagType = typeSettings.EventStreamTagType,
                    ActiveDocumentRefType = typeSettings.DocumentRefType,
                    ActiveDataStore = targetStore,
                    ActiveDocumentStore = targetStore,
                    ActiveDocumentTagStore = tableSettings.DefaultDocumentTagStore,
                    ActiveStreamTagStore = tableSettings.DefaultDocumentTagStore,
                    ActiveSnapShotStore = tableSettings.DefaultSnapShotStore,
                    ActiveChunkingEnabled = tableSettings.EnableStreamChunks,
                    ActiveChunkSize = tableSettings.DefaultChunkSize,

                    SchemaVersion = "1.0.0"
                };

                await documentTableClient.AddEntityAsync(newEntity);

                // Create initial chunk if chunking is enabled
                if (tableSettings.EnableStreamChunks)
                {
                    var chunkTableClient = await GetTableClientAsync(targetStore, tableSettings.DefaultStreamChunkTableName);
                    var chunkEntity = new TableStreamChunkEntity
                    {
                        PartitionKey = objectId,
                        RowKey = $"{0:d10}",
                        ChunkIdentifier = 0,
                        FirstEventVersion = 0,
                        LastEventVersion = -1
                    };
                    await chunkTableClient.AddEntityAsync(chunkEntity);
                }
            }

            // Retrieve the entity to return
            var entity = await documentTableClient.GetEntityAsync<TableDocumentEntity>(partitionKey, rowKey);
            return await ToObjectDocumentAsync(entity.Value, targetStore);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new TableDocumentStoreTableNotFoundException(
                $"The table '{tableSettings.DefaultDocumentTableName}' is not found. " +
                "Please create it or enable AutoCreateTable in settings.", ex);
        }
    }

    /// <summary>
    /// Retrieves and materializes the object document from its table entity.
    /// </summary>
    /// <param name="name">The object name used to determine the partition key.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetAsync(
        string name,
        string objectId,
        string? store = null)
    {
        var targetStore = store ?? tableSettings.DefaultDocumentStore;
        var tableClient = await GetTableClientAsync(targetStore, tableSettings.DefaultDocumentTableName);

        var partitionKey = name.ToLowerInvariant();
        var rowKey = objectId;

        try
        {
            var entity = await tableClient.GetEntityAsync<TableDocumentEntity>(partitionKey, rowKey);
            return await ToObjectDocumentAsync(entity.Value, targetStore);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "TableNotFound")
        {
            throw new TableDocumentStoreTableNotFoundException(
                $"The table '{tableSettings.DefaultDocumentTableName}' is not found. " +
                "Please create it or enable AutoCreateTable in settings.", ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new TableDocumentNotFoundException(
                $"The object document for object '{name}' by the id '{objectId}' was not found in store '{targetStore}'.", ex);
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
        var targetDocumentTagStore = documentTagStore ?? tableSettings.DefaultDocumentTagStore;
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
        var targetDocumentTagStore = documentTagStore ?? tableSettings.DefaultDocumentTagStore;
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
    /// Persists the provided object document to table storage, updating its hash for optimistic concurrency.
    /// </summary>
    /// <param name="document">The document to persist.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SetAsync(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var documentStore = GetDocumentConnectionName(document);
        var documentTableClient = await GetTableClientAsync(documentStore, tableSettings.DefaultDocumentTableName);

        var partitionKey = document.ObjectName.ToLowerInvariant();
        var rowKey = document.ObjectId;

        // Compute hash from active stream data
        var hashInput = string.Join("|",
            document.Active.StreamIdentifier,
            document.Active.StreamType,
            document.Active.CurrentStreamVersion,
            document.Active.DataStore,
            document.Active.DocumentStore);
        var hash = ComputeSha256Hash(hashInput);

        var entity = new TableDocumentEntity
        {
            PartitionKey = partitionKey,
            RowKey = rowKey,
            ObjectId = document.ObjectId,
            ObjectName = document.ObjectName,

            // Individual Active stream columns
            ActiveStreamIdentifier = document.Active.StreamIdentifier,
            ActiveStreamType = document.Active.StreamType,
            ActiveDocumentTagType = document.Active.DocumentTagType,
            ActiveCurrentStreamVersion = document.Active.CurrentStreamVersion,
            ActiveDocumentType = document.Active.DocumentType,
            ActiveEventStreamTagType = document.Active.EventStreamTagType,
            ActiveDocumentRefType = document.Active.DocumentRefType,
            ActiveDataStore = document.Active.DataStore,
            ActiveDocumentStore = document.Active.DocumentStore,
            ActiveDocumentTagStore = document.Active.DocumentTagStore,
            ActiveStreamTagStore = document.Active.StreamTagStore,
            ActiveSnapShotStore = document.Active.SnapShotStore,
            ActiveChunkingEnabled = document.Active.ChunkSettings?.EnableChunks ?? false,
            ActiveChunkSize = document.Active.ChunkSettings?.ChunkSize ?? tableSettings.DefaultChunkSize,

            SchemaVersion = document.SchemaVersion ?? "1.0.0",
            Hash = hash,
            PrevHash = document.Hash
        };

        try
        {
            // Try to get existing entity for ETag
            var existingResponse = await documentTableClient.GetEntityIfExistsAsync<TableDocumentEntity>(partitionKey, rowKey);
            if (existingResponse.HasValue)
            {
                entity.ETag = existingResponse.Value!.ETag;
                await documentTableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
            }
            else
            {
                await documentTableClient.AddEntityAsync(entity);
            }

            // Save stream chunks to separate table
            await SaveStreamChunksAsync(document, documentStore);

            // Save snapshots to separate table
            await SaveSnapShotsAsync(document, documentStore);

            // Save terminated streams to separate table
            await SaveTerminatedStreamsAsync(document, documentStore);

            document.SetHash(hash, document.Hash);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            throw new TableDataStoreProcessingException(
                $"Optimistic concurrency conflict when updating document '{document.ObjectName}/{document.ObjectId}'.", ex);
        }
    }

    private async Task SaveStreamChunksAsync(IObjectDocument document, string store)
    {
        var chunkTableClient = await GetTableClientAsync(store, tableSettings.DefaultStreamChunkTableName);

        foreach (var chunk in document.Active.StreamChunks)
        {
            var chunkEntity = new TableStreamChunkEntity
            {
                PartitionKey = document.ObjectId,
                RowKey = $"{chunk.ChunkIdentifier:d10}",
                ChunkIdentifier = chunk.ChunkIdentifier,
                FirstEventVersion = chunk.FirstEventVersion,
                LastEventVersion = chunk.LastEventVersion
            };

            await chunkTableClient.UpsertEntityAsync(chunkEntity, TableUpdateMode.Replace);
        }
    }

    private async Task SaveSnapShotsAsync(IObjectDocument document, string store)
    {
        var snapShotTableClient = await GetTableClientAsync(store, tableSettings.DefaultDocumentSnapShotTableName);

        foreach (var snapShot in document.Active.SnapShots)
        {
            var snapShotEntity = new TableDocumentSnapShotEntity
            {
                PartitionKey = document.ObjectId,
                RowKey = $"{snapShot.UntilVersion:d10}",
                UntilVersion = snapShot.UntilVersion,
                Name = snapShot.Name
            };

            await snapShotTableClient.UpsertEntityAsync(snapShotEntity, TableUpdateMode.Replace);
        }
    }

    private async Task SaveTerminatedStreamsAsync(IObjectDocument document, string store)
    {
        var terminatedStreamTableClient = await GetTableClientAsync(store, tableSettings.DefaultTerminatedStreamTableName);

        foreach (var terminatedStream in document.TerminatedStreams)
        {
            if (string.IsNullOrEmpty(terminatedStream.StreamIdentifier))
            {
                continue;
            }

            var terminatedStreamEntity = new TableTerminatedStreamEntity
            {
                PartitionKey = document.ObjectId,
                RowKey = terminatedStream.StreamIdentifier,
                StreamIdentifier = terminatedStream.StreamIdentifier,
                StreamType = terminatedStream.StreamType,
                DataStore = terminatedStream.StreamConnectionName,
                Reason = terminatedStream.Reason,
                ContinuationStreamId = terminatedStream.ContinuationStreamId,
                TerminationDate = terminatedStream.TerminationDate,
                StreamVersion = terminatedStream.StreamVersion,
                Deleted = terminatedStream.Deleted,
                DeletionDate = terminatedStream.DeletionDate
            };

            await terminatedStreamTableClient.UpsertEntityAsync(terminatedStreamEntity, TableUpdateMode.Replace);
        }
    }

    private async Task<TableClient> GetTableClientAsync(string connectionName, string tableName)
    {
        var serviceClient = clientFactory.CreateClient(connectionName);
        var tableClient = serviceClient.GetTableClient(tableName);

        if (tableSettings.AutoCreateTable)
        {
            await tableClient.CreateIfNotExistsAsync();
        }

        return tableClient;
    }

    private async Task<TableEventStreamDocument> ToObjectDocumentAsync(TableDocumentEntity entity, string store)
    {
        // Load stream chunks from separate table
        var streamChunks = await LoadStreamChunksAsync(entity.ObjectId, store);

        // Load snapshots from separate table
        var snapShots = await LoadSnapShotsAsync(entity.ObjectId, store);

        // Load terminated streams from separate table
        var terminatedStreams = await LoadTerminatedStreamsAsync(entity.ObjectId, store);

        // Build ChunkSettings from entity
        StreamChunkSettings? chunkSettings = entity.ActiveChunkingEnabled
            ? new StreamChunkSettings
            {
                EnableChunks = entity.ActiveChunkingEnabled,
                ChunkSize = entity.ActiveChunkSize
            }
            : null;

        var streamInfo = new StreamInformation
        {
            StreamIdentifier = entity.ActiveStreamIdentifier,
            StreamType = entity.ActiveStreamType,
            DocumentTagType = entity.ActiveDocumentTagType,
            CurrentStreamVersion = entity.ActiveCurrentStreamVersion,
            DocumentType = entity.ActiveDocumentType,
            EventStreamTagType = entity.ActiveEventStreamTagType,
            DocumentRefType = entity.ActiveDocumentRefType,
            DataStore = entity.ActiveDataStore,
            DocumentStore = entity.ActiveDocumentStore,
            DocumentTagStore = entity.ActiveDocumentTagStore,
            StreamTagStore = entity.ActiveStreamTagStore,
            SnapShotStore = entity.ActiveSnapShotStore,
            ChunkSettings = chunkSettings,
            StreamChunks = streamChunks,
            SnapShots = snapShots
        };

        var doc = new TableEventStreamDocument(
            entity.ObjectId,
            entity.ObjectName,
            streamInfo,
            terminatedStreams,
            entity.SchemaVersion,
            entity.Hash,
            entity.PrevHash);

        return doc;
    }

    private async Task<List<StreamChunk>> LoadStreamChunksAsync(string objectId, string store)
    {
        var chunks = new List<StreamChunk>();
        var chunkTableClient = await GetTableClientAsync(store, tableSettings.DefaultStreamChunkTableName);

        try
        {
            await foreach (var entity in chunkTableClient.QueryAsync<TableStreamChunkEntity>(
                filter: $"PartitionKey eq '{objectId}'"))
            {
                chunks.Add(new StreamChunk(
                    entity.ChunkIdentifier,
                    entity.FirstEventVersion,
                    entity.LastEventVersion));
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table doesn't exist yet, return empty list
        }

        return chunks.OrderBy(c => c.ChunkIdentifier).ToList();
    }

    private async Task<List<StreamSnapShot>> LoadSnapShotsAsync(string objectId, string store)
    {
        var snapShots = new List<StreamSnapShot>();
        var snapShotTableClient = await GetTableClientAsync(store, tableSettings.DefaultDocumentSnapShotTableName);

        try
        {
            await foreach (var entity in snapShotTableClient.QueryAsync<TableDocumentSnapShotEntity>(
                filter: $"PartitionKey eq '{objectId}'"))
            {
                snapShots.Add(new StreamSnapShot
                {
                    UntilVersion = entity.UntilVersion,
                    Name = entity.Name
                });
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table doesn't exist yet, return empty list
        }

        return snapShots.OrderBy(s => s.UntilVersion).ToList();
    }

    private async Task<List<TerminatedStream>> LoadTerminatedStreamsAsync(string objectId, string store)
    {
        var terminatedStreams = new List<TerminatedStream>();
        var terminatedStreamTableClient = await GetTableClientAsync(store, tableSettings.DefaultTerminatedStreamTableName);

        try
        {
            await foreach (var entity in terminatedStreamTableClient.QueryAsync<TableTerminatedStreamEntity>(
                filter: $"PartitionKey eq '{objectId}'"))
            {
                terminatedStreams.Add(new TerminatedStream
                {
                    StreamIdentifier = entity.StreamIdentifier,
                    StreamType = entity.StreamType,
                    StreamConnectionName = entity.DataStore,
                    Reason = entity.Reason,
                    ContinuationStreamId = entity.ContinuationStreamId,
                    TerminationDate = entity.TerminationDate,
                    StreamVersion = entity.StreamVersion,
                    Deleted = entity.Deleted,
                    DeletionDate = entity.DeletionDate
                });
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table doesn't exist yet, return empty list
        }

        return terminatedStreams;
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

    private string GetDocumentConnectionName(IObjectDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.Active.DocumentStore))
        {
            return document.Active.DocumentStore;
        }

        return tableSettings.DefaultDocumentStore;
    }
}
