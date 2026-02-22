using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.AppendBlob.Model;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Validation;
using Microsoft.Extensions.Azure;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErikLieben.FA.ES.Configuration;
using SerializeAppendBlobEventStreamDocumentContext = ErikLieben.FA.ES.AzureStorage.AppendBlob.Model.SerializeAppendBlobEventStreamDocumentContext;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob;

/// <summary>
/// Provides Azure Blob Storage backed persistence for object documents and their stream metadata
/// in the Append Blob storage provider.
/// </summary>
public class AppendBlobDocumentStore : IAppendBlobDocumentStore
{
    private static readonly ConcurrentDictionary<string, bool> VerifiedContainers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly EventStreamAppendBlobSettings appendBlobSettings;
    private readonly IDocumentTagDocumentFactory documentTagStoreFactory;
    private readonly EventStreamDefaultTypeSettings typeSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppendBlobDocumentStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="documentTagStoreFactory">The factory used to access document tag storage.</param>
    /// <param name="appendBlobSettings">The append blob storage settings used for containers.</param>
    /// <param name="typeSettings">The default type settings for streams, documents, and tags.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public AppendBlobDocumentStore(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagStoreFactory,
        EventStreamAppendBlobSettings appendBlobSettings,
        EventStreamDefaultTypeSettings typeSettings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(appendBlobSettings);
        ArgumentNullException.ThrowIfNull(documentTagStoreFactory);
        ArgumentNullException.ThrowIfNull(typeSettings);

        this.clientFactory = clientFactory;
        this.appendBlobSettings = appendBlobSettings;
        this.documentTagStoreFactory = documentTagStoreFactory;
        this.typeSettings = typeSettings;
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
        ObjectIdValidator.Validate(objectId);

        var documentPath = $"{name}/{objectId}.json";
        var targetStore = store ?? appendBlobSettings.DefaultDocumentStore;
        var blob = await CreateBlobClientAsync(targetStore, appendBlobSettings.DefaultDocumentContainerName, documentPath);

        bool isNewDocument = false;
        string? streamIdentifier = null;
        try
        {
            if (!await blob.ExistsAsync())
            {
                isNewDocument = true;
                streamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000";

                var newDocument = new SerializeAppendBlobEventStreamDocument
                {
                    ObjectId = objectId,
                    ObjectName = name,
                    TerminatedStreams = [],
                    Active = new SerializeStreamInformation
                    {
                        StreamIdentifier = streamIdentifier,
                        StreamType = typeSettings.StreamType,
                        DocumentType = typeSettings.DocumentType,
                        DocumentTagType = typeSettings.DocumentTagType,
                        EventStreamTagType = typeSettings.EventStreamTagType,
                        DocumentRefType = typeSettings.DocumentRefType,
                        CurrentStreamVersion = -1,
                        DocumentStore = targetStore,
                        DataStore = targetStore,
                        DocumentTagStore = appendBlobSettings.DefaultDocumentTagStore,
                        StreamTagStore = appendBlobSettings.DefaultDocumentTagStore,
                        SnapShotStore = appendBlobSettings.DefaultSnapShotStore,
                        ChunkSettings = appendBlobSettings.EnableStreamChunks
                            ? new Documents.StreamChunkSettings
                            {
                                EnableChunks = true,
                                ChunkSize = appendBlobSettings.DefaultChunkSize
                            }
                            : null,
                        StreamChunks = appendBlobSettings.EnableStreamChunks
                            ? [new Documents.StreamChunk(0, 0, -1)]
                            : []
                    }
                };
                await blob.SaveEntityAsync(newDocument, SerializeAppendBlobEventStreamDocumentContext.Default.SerializeAppendBlobEventStreamDocument);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "ContainerNotFound")
        {
            throw new BlobDocumentStoreContainerNotFoundException(
                $"The container by the name '{appendBlobSettings.DefaultDocumentContainerName}' is not found. " +
                "Please create it or adjust the config setting: 'EventStream:AppendBlob:DefaultDocumentContainerName'",
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
        var doc = JsonSerializer.Deserialize(json, DeserializeAppendBlobEventStreamDocumentContext.Default.DeserializeAppendBlobEventStreamDocument);

        if (doc == null)
        {
            return null!;
        }
        var newDoc = ToAppendBlobEventStreamDocument(doc);

        var hash = ComputeSha256Hash(json);
        newDoc.SetHash(hash, hash);

        // For new documents, create the initial append blob with a commit marker
        // to prevent first-write races (IfAppendPositionEqual=0 ensures only one writer wins)
        if (isNewDocument && streamIdentifier != null)
        {
            await CreateInitialAppendBlobAsync(targetStore, name, streamIdentifier, hash);
        }

        return newDoc;
    }

    private static AppendBlobEventStreamDocument ToAppendBlobEventStreamDocument(DeserializeAppendBlobEventStreamDocument doc)
    {
        return doc.ToAppendBlobEventStreamDocument();
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
        ObjectIdValidator.Validate(objectId);

        var documentPath = $"{name}/{objectId}.json";
        var targetStore = store ?? appendBlobSettings.DefaultDocumentStore;
        var blob = await CreateBlobClientAsync(targetStore, appendBlobSettings.DefaultDocumentContainerName, documentPath);

        ETag? etag;
        try
        {
            var properties = await blob.GetPropertiesAsync();
            etag = properties.Value.ETag;
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "ContainerNotFound")
        {
            throw new BlobDocumentStoreContainerNotFoundException(
                $"The container by the name '{appendBlobSettings.DefaultDocumentContainerName}' is not found. " +
                "Please create it or adjust the config setting: 'EventStream:AppendBlob:DefaultDocumentContainerName'",
                ex);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "BlobNotFound")
        {
            throw new BlobDocumentNotFoundException(
                $"The object document for object '{name}' by the id '{objectId}' was not found in store '{appendBlobSettings.DefaultDocumentStore}'. " +
                "Please create it or adjust the config setting: 'EventStream:AppendBlob:DefaultDocumentContainerName'",
                ex);
        }

        // Download content with same etag
        var (doc, hash) = await blob.AsEntityAsync(
            DeserializeAppendBlobEventStreamDocumentContext.Default.DeserializeAppendBlobEventStreamDocument,
            new BlobRequestConditions { IfMatch = etag });
        if (doc == null)
        {
            return null!;
        }
        var newDoc = ToAppendBlobEventStreamDocument(doc);
        newDoc.SetHash(hash, hash);
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
        var targetDocumentTagStore = documentTagStore ?? this.appendBlobSettings.DefaultDocumentTagStore;
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
        var targetDocumentTagStore = documentTagStore ?? this.appendBlobSettings.DefaultDocumentTagStore;
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
        var documentStore = GetDocumentConnectionName(document);
        var blob = await CreateBlobClientAsync(documentStore, appendBlobSettings.DefaultDocumentContainerName, documentPath);

        // Use SerializeAppendBlobEventStreamDocument to exclude legacy *ConnectionName properties
        var serializeDoc = SerializeAppendBlobEventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(serializeDoc);

        // Try to get properties, but handle the case where blob doesn't exist yet
        ETag? etag = null;
        bool isNewBlob = false;
        try
        {
            var properties = await blob.GetPropertiesAsync();
            var etagRetrieved = properties.Value.ETag.ToString().Replace("\u0022", string.Empty);
            etag = string.IsNullOrEmpty(etagRetrieved) ? null : new ETag(etagRetrieved);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "BlobNotFound")
        {
            // Blob doesn't exist yet - use IfNoneMatch to prevent concurrent creation clobber
            isNewBlob = true;
        }

        var conditions = isNewBlob
            ? new BlobRequestConditions { IfNoneMatch = new ETag("*") }
            : new BlobRequestConditions { IfMatch = etag };

        var (_, hash) = await blob.SaveEntityAsync(serializeDoc, SerializeAppendBlobEventStreamDocumentContext.Default.SerializeAppendBlobEventStreamDocument,
            conditions);

        document.SetHash(hash, document.Hash);
    }

    /// <summary>
    /// Creates the initial append blob with a commit marker at position 0.
    /// Uses IfAppendPositionEqual=0 to prevent duplicate initial writes from concurrent creators.
    /// </summary>
    private async Task CreateInitialAppendBlobAsync(string connectionName, string objectName, string streamIdentifier, string documentHash)
    {
        var client = clientFactory.CreateClient(connectionName);
        var container = client.GetBlobContainerClient(objectName.ToLowerInvariant());

        var containerCacheKey = $"{connectionName}:{objectName.ToLowerInvariant()}";
        if (appendBlobSettings.AutoCreateContainer && VerifiedContainers.TryAdd(containerCacheKey, true))
        {
            await container.CreateIfNotExistsAsync();
        }

        var blobPath = appendBlobSettings.EnableStreamChunks
            ? $"{streamIdentifier}-0000000000.ndjson"
            : $"{streamIdentifier}.ndjson";
        var appendBlob = container.GetAppendBlobClient(blobPath);

        await appendBlob.CreateIfNotExistsAsync(
            new AppendBlobCreateOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/x-ndjson" }
            });

        var marker = new AppendBlobCommitMarker
        {
            Hash = documentHash,
            PrevHash = "*",
            Version = 0,
            Offset = 0
        };

        var markerLine = JsonSerializer.Serialize(marker, AppendBlobCommitMarkerContext.Default.AppendBlobCommitMarker) + "\n";
        var markerBytes = Encoding.UTF8.GetBytes(markerLine);

        try
        {
            using var markerStream = new MemoryStream(markerBytes);
            await appendBlob.AppendBlockAsync(
                markerStream,
                new AppendBlobAppendBlockOptions
                {
                    Conditions = new AppendBlobRequestConditions { IfAppendPositionEqual = 0 }
                });
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Another writer created the initial marker first - this is fine
        }
    }

    private async Task<BlobClient> CreateBlobClientAsync(
        string connectionName,
        string objectDocumentContainerName,
        string documentPath)
    {
        var client = clientFactory.CreateClient(connectionName);

        var containerNameLower = objectDocumentContainerName.ToLowerInvariant();
        var container = client.GetBlobContainerClient(containerNameLower);
        var cacheKey = $"{connectionName}:{containerNameLower}";
        if (appendBlobSettings.AutoCreateContainer && VerifiedContainers.TryAdd(cacheKey, true))
        {
            await container.CreateIfNotExistsAsync();
        }
        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");
        return blob!;
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

    /// <summary>
    /// Gets the document connection name from the document's active stream, falling back to the default if not configured.
    /// </summary>
    /// <param name="document">The document to retrieve the connection name from.</param>
    /// <returns>The configured connection name or the default document store.</returns>
    private string GetDocumentConnectionName(IObjectDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.Active.DocumentStore))
        {
            return document.Active.DocumentStore;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (!string.IsNullOrWhiteSpace(document.Active.DocumentConnectionName))
        {
            return document.Active.DocumentConnectionName;
        }
#pragma warning restore CS0618

        return appendBlobSettings.DefaultDocumentStore;
    }

    /// <summary>
    /// Clears the verified containers cache. Used in tests to prevent cross-test pollution.
    /// </summary>
    public static void ClearVerifiedContainersCache() => VerifiedContainers.Clear();
}
