using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.Configuration;
using BlobEventStreamDocumentContext = ErikLieben.FA.ES.AzureStorage.Blob.Model.BlobEventStreamDocumentContext;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Provides Azure Blob Storage backed persistence for object documents and their stream metadata.
/// </summary>
public class BlobDocumentStore : IBlobDocumentStore
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly EventStreamBlobSettings blobSettings;
    private readonly IDocumentTagDocumentFactory documentTagStoreFactory;

    /// <summary>
/// Initializes a new instance of the <see cref="BlobDocumentStore"/> class.
/// </summary>
/// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
/// <param name="documentTagStoreFactory">The factory used to access document tag storage.</param>
/// <param name="blobSettings">The blob storage settings used for containers and chunking.</param>
/// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
public BlobDocumentStore(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagStoreFactory,
        EventStreamBlobSettings blobSettings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(blobSettings);
        ArgumentNullException.ThrowIfNull(documentTagStoreFactory);

        this.clientFactory = clientFactory;
        this.blobSettings = blobSettings;
        this.documentTagStoreFactory = documentTagStoreFactory;
    }

    /// <summary>
/// Creates a new document blob with initialized stream metadata if missing; returns the materialized document.
/// </summary>
/// <param name="name">The object name used to determine the container and path.</param>
/// <param name="objectId">The identifier of the object to create.</param>
/// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
/// <returns>The created or existing object document loaded from storage.</returns>
/// <exception cref="BlobDocumentStoreContainerNotFoundException">Thrown when the configured document container does not exist.</exception>
[return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public async Task<IObjectDocument> CreateAsync(
        string name,
        string objectId,
        string? store = null)
    {
        var documentPath = $"{name}/{objectId}.json";
        var targetStore = store ?? blobSettings.DefaultDocumentStore;
        var blob = CreateBlobClient(targetStore, blobSettings.DefaultDocumentContainerName, documentPath);

        try
        {
            if (!await blob.ExistsAsync())
            {
                await blob.SaveEntityAsync(
                    new BlobEventStreamDocument(
                        objectId,
                        name,
                        new StreamInformation
                        {
                            StreamConnectionName = targetStore,
                            SnapShotConnectionName = blobSettings.DefaultSnapShotStore,
                            DocumentTagConnectionName = blobSettings.DefaultDocumentTagStore,
                            StreamTagConnectionName = blobSettings.DefaultDocumentTagStore,
                            StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
                            StreamType = "blob",
                            DocumentTagType = "blob",
                            CurrentStreamVersion = -1,
                            // Initialize store settings from the target store
                            DocumentStore = targetStore,
                            DataStore = targetStore,
                            ChunkSettings = blobSettings.EnableStreamChunks
                                ? new StreamChunkSettings
                                {
                                    EnableChunks = blobSettings.EnableStreamChunks,
                                    ChunkSize = blobSettings.DefaultChunkSize,
                                }
                                : null,
                            StreamChunks = blobSettings.EnableStreamChunks
                                ?
                                [
                                    new(chunkIdentifier: 0, firstEventVersion: 0, lastEventVersion: -1)
                                ]
                                : []
                        }, []),
                BlobEventStreamDocumentContext.Default.BlobEventStreamDocument);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "ContainerNotFound")
        {
            throw new BlobDocumentStoreContainerNotFoundException(
                $"The container by the name '{blobSettings.DefaultDocumentContainerName}' is not found. " +
                "Please create it or adjust the config setting: 'EventStream:Blob:DefaultDocumentContainerName'",
                ex);
        }

        var properties = await blob.GetPropertiesAsync();
        var etag = properties.Value.ETag;

        // Download content with same etag
        var json = await blob.AsString(new BlobRequestConditions { IfMatch = etag });
        if (string.IsNullOrEmpty(json))
        {
            return null!;
        }
        var doc = JsonSerializer.Deserialize(json, DeserializeBlobEventStreamDocumentContext.Default.DeserializeBlobEventStreamDocument);

        if (doc == null)
        {
            return null!;
        }
        var newDoc = ToBlobEventStreamDocument(doc);

        newDoc.SetHash(ComputeSha256Hash(json), ComputeSha256Hash(json));
        return newDoc;
    }

    private static BlobEventStreamDocument ToBlobEventStreamDocument(DeserializeBlobEventStreamDocument doc)
    {
        return new BlobEventStreamDocument(
            doc.ObjectId,
            doc.ObjectName,
            doc.Active,
            doc.TerminatedStreams,
            doc.SchemaVersion,
            doc.Hash,
            doc.PrevHash,
            doc.DocumentPath);
    }

    /// <summary>
/// Retrieves and materializes the object document from its blob using the configured serializers.
/// </summary>
/// <param name="name">The object name used to determine the container and path.</param>
/// <param name="objectId">The identifier of the object to retrieve.</param>
/// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
/// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
/// <exception cref="BlobDocumentNotFoundException">Thrown when the document blob cannot be found.</exception>
public async Task<IObjectDocument> GetAsync(
        string name,
        string objectId,
        string? store = null)
    {

        var documentPath = $"{name}/{objectId}.json";
        var targetStore = store ?? blobSettings.DefaultDocumentStore;
        var blob = CreateBlobClient(targetStore, blobSettings.DefaultDocumentContainerName, documentPath);

        ETag? etag;
        try
        {
            var properties = await blob.GetPropertiesAsync();
            etag = properties.Value.ETag;
        }
        // We're not creating the container or trying to create it each time,
        // return a more detailed Exception so the configuration can be adjusted/ fixed
        // in the case we're unable to find it.
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "ContainerNotFound")
        {
            throw new BlobDocumentStoreContainerNotFoundException(
                $"The container by the name '{blobSettings.DefaultDocumentContainerName}' is not found. " +
                "Please create it or adjust the config setting: 'EventStream:Blob:DefaultDocumentContainerName'",
                ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "BlobNotFound")
        {
            throw new BlobDocumentNotFoundException(
                $"The object document for object '{name}' by the id '{objectId}' was not found in store '{blobSettings.DefaultDocumentStore}'. " +
                "Please create it or adjust the config setting: 'EventStream:Blob:DefaultDocumentContainerName'",
                ex);
        }

        // Download content with same etag
        var (doc, hash) = await blob.AsEntityAsync(
            DeserializeBlobEventStreamDocumentContext.Default.DeserializeBlobEventStreamDocument,
            new BlobRequestConditions { IfMatch = etag });
        if (doc == null)
        {
            return null!;
        }
        var newDoc = ToBlobEventStreamDocument(doc);
        newDoc.SetHash(hash,hash);
        newDoc.DocumentPath = documentPath;
        return newDoc;
    }

    /// <summary>
    /// Retrieves the first document matching the given document tag from the tag store and loads it from blob storage.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="tag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the document. If not provided, uses the default document store.</param>
    /// <returns>The first matching document or null if no document matches.</returns>
    public async Task<IObjectDocument?> GetFirstByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null)
    {
        var targetDocumentTagStore = documentTagStore ?? this.blobSettings.DefaultDocumentTagStore;
        var documentTagStoreInstance = documentTagStoreFactory.CreateDocumentTagStore(targetDocumentTagStore);
        var objectId = (await documentTagStoreInstance.GetAsync(objectName, tag)).FirstOrDefault();
        if (!string.IsNullOrEmpty(objectId))
        {
            return await GetAsync(objectName, objectId, store);
        }
        return null;
    }


    /// <summary>
    /// Retrieves all documents matching the given document tag from the tag store and loads them from blob storage.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="tag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the documents. If not provided, uses the default document store.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null)
    {
        var targetDocumentTagStore = documentTagStore ?? this.blobSettings.DefaultDocumentTagStore;
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
    /// Persists the provided object document JSON to blob storage, updating its hash for optimistic concurrency.
    /// </summary>
    /// <param name="document">The document to persist.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SetAsync(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var documentPath = $"{document.ObjectName}/{document.ObjectId}.json";

        // Use document-specific store if configured, otherwise fall back to default
        var documentStore = GetDocumentStore(document);
        var blob = CreateBlobClient(documentStore, blobSettings.DefaultDocumentContainerName, documentPath);

        var blobDoc = BlobEventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(blobDoc);

        // Try to get properties, but handle the case where blob doesn't exist yet
        ETag? etag = null;
        try
        {
            var properties = await blob.GetPropertiesAsync();
            var etagRetrieved = properties.Value.ETag.ToString().Replace("\u0022", string.Empty);
            etag = string.IsNullOrEmpty(etagRetrieved) ? null : new ETag(etagRetrieved);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "BlobNotFound")
        {
            // Blob doesn't exist yet - this is fine for new documents being created with custom store settings
            etag = null;
        }

        var (_, hash) = await blob.SaveEntityAsync(blobDoc, BlobEventStreamDocumentContext.Default.BlobEventStreamDocument,
            new BlobRequestConditions { IfMatch = etag });

        document.SetHash(hash,blobDoc.Hash);
    }

    private BlobClient CreateBlobClient(
        string connectionName,
        string objectDocumentContainerName,
        string documentPath)
    {
        var client = clientFactory.CreateClient(connectionName);

        var container = client.GetBlobContainerClient(objectDocumentContainerName.ToLowerInvariant());
        if (blobSettings.AutoCreateContainer)
        {
            container.CreateIfNotExists();
        }
        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");
        return blob!;
    }

    private static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        StringBuilder builder = new();
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Updates the active stream configuration of an object document and synchronizes the
    /// <c>LastObjectDocumentHash</c> stored in the corresponding stream document so that
    /// subsequent append operations pass the optimistic concurrency check.
    /// </summary>
    /// <param name="name">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to update.</param>
    /// <param name="activeConfiguration">The new active stream configuration to apply.</param>
    /// <param name="store">Optional store name override for loading the object document. If not provided, uses the default document store.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public Task UpdateActiveConfigurationAsync(
        string name,
        string objectId,
        StreamInformation activeConfiguration,
        string? store = null)
    {
        ArgumentNullException.ThrowIfNull(activeConfiguration);

        return UpdateActiveConfigurationCoreAsync(name, objectId, active =>
        {
            active.StreamIdentifier = activeConfiguration.StreamIdentifier;
            active.StreamType = activeConfiguration.StreamType;
            active.DocumentTagType = activeConfiguration.DocumentTagType;
            active.CurrentStreamVersion = activeConfiguration.CurrentStreamVersion;
            active.StreamConnectionName = activeConfiguration.StreamConnectionName;
            active.DocumentTagConnectionName = activeConfiguration.DocumentTagConnectionName;
            active.StreamTagConnectionName = activeConfiguration.StreamTagConnectionName;
            active.SnapShotConnectionName = activeConfiguration.SnapShotConnectionName;
            active.ChunkSettings = activeConfiguration.ChunkSettings;
            active.StreamChunks = activeConfiguration.StreamChunks;
            active.SnapShots = activeConfiguration.SnapShots;
            active.DocumentType = activeConfiguration.DocumentType;
            active.EventStreamTagType = activeConfiguration.EventStreamTagType;
            active.DocumentRefType = activeConfiguration.DocumentRefType;
            active.DataStore = activeConfiguration.DataStore;
            active.DocumentStore = activeConfiguration.DocumentStore;
            active.DocumentTagStore = activeConfiguration.DocumentTagStore;
            active.StreamTagStore = activeConfiguration.StreamTagStore;
            active.SnapShotStore = activeConfiguration.SnapShotStore;
        }, store);
    }

    /// <summary>
    /// Updates the active stream configuration of an object document by applying the specified
    /// modifications and synchronizes the <c>LastObjectDocumentHash</c> stored in the corresponding
    /// stream document so that subsequent append operations pass the optimistic concurrency check.
    /// </summary>
    /// <param name="name">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to update.</param>
    /// <param name="configure">An action that receives the current <see cref="StreamInformation"/> for in-place modification.</param>
    /// <param name="store">Optional store name override for loading the object document. If not provided, uses the default document store.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public Task UpdateActiveConfigurationAsync(
        string name,
        string objectId,
        Action<StreamInformation> configure,
        string? store = null)
    {
        ArgumentNullException.ThrowIfNull(configure);

        return UpdateActiveConfigurationCoreAsync(name, objectId, configure, store);
    }

    private async Task UpdateActiveConfigurationCoreAsync(
        string name,
        string objectId,
        Action<StreamInformation> configure,
        string? store)
    {
        // Load the current document
        var document = await GetAsync(name, objectId, store);

        // Capture old stream location before modifying Active so we can
        // locate the existing stream blob that holds LastObjectDocumentHash.
        var oldStreamConnectionName = document.Active.StreamConnectionName;
        var oldStreamIdentifier = document.Active.StreamIdentifier;
        var oldChunkingEnabled = document.Active.ChunkingEnabled();
        var oldStreamChunks = document.Active.StreamChunks.ToList();

        // Apply the caller's modifications to the active stream
        configure(document.Active);

        // Save the object document — this produces a new content hash
        await SetAsync(document);

        // Update the stream document's LastObjectDocumentHash using the OLD
        // stream location (the stream blob hasn't moved).
        string streamDocumentPath;
        if (oldChunkingEnabled && oldStreamChunks.Count > 0)
        {
            var lastChunk = oldStreamChunks[^1];
            streamDocumentPath = $"{oldStreamIdentifier}-{lastChunk.ChunkIdentifier:d10}.json";
        }
        else
        {
            streamDocumentPath = $"{oldStreamIdentifier}.json";
        }

        var client = clientFactory.CreateClient(oldStreamConnectionName);
        var container = client.GetBlobContainerClient(name.ToLowerInvariant());
        var streamBlob = container.GetBlobClient(streamDocumentPath);

        if (!await streamBlob.ExistsAsync())
        {
            // No stream document yet (no events appended) — nothing to synchronize
            return;
        }

        var properties = await streamBlob.GetPropertiesAsync();
        var etag = properties.Value.ETag;

        var (streamDoc, _) = await streamBlob.AsEntityAsync(
            BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
            new BlobRequestConditions { IfMatch = etag });

        if (streamDoc == null)
        {
            return;
        }

        streamDoc.LastObjectDocumentHash = document.Hash ?? "*";
        await streamBlob.SaveEntityAsync(
            streamDoc,
            BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
            new BlobRequestConditions { IfMatch = etag });
    }

    /// <summary>
    /// Gets the document store name from the document's active stream, falling back to the default if not configured.
    /// </summary>
    /// <param name="document">The document to retrieve the store setting from.</param>
    /// <returns>The configured store name or the default document store.</returns>
    private string GetDocumentStore(IObjectDocument document)
    {
        return !string.IsNullOrWhiteSpace(document.Active.DocumentStore)
            ? document.Active.DocumentStore
            : blobSettings.DefaultDocumentStore;
    }
}
