using System.Net;
using System.Text.Json;
using Amazon.S3;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Extensions;
using ErikLieben.FA.ES.S3.Model;
using SerializeS3EventStreamDocumentContext = ErikLieben.FA.ES.S3.Model.SerializeS3EventStreamDocumentContext;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Provides S3-compatible storage backed persistence for object documents and their stream metadata.
/// </summary>
public class S3DocumentStore : IS3DocumentStore
{
    private readonly IS3ClientFactory clientFactory;
    private readonly EventStreamS3Settings s3Settings;
    private readonly IDocumentTagDocumentFactory documentTagStoreFactory;
    private readonly EventStreamDefaultTypeSettings typeSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3DocumentStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory used to create <see cref="IAmazonS3"/> instances.</param>
    /// <param name="documentTagStoreFactory">The factory used to access document tag storage.</param>
    /// <param name="s3Settings">The S3 storage settings used for buckets and chunking.</param>
    /// <param name="typeSettings">The default type settings for streams, documents, and tags.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public S3DocumentStore(
        IS3ClientFactory clientFactory,
        IDocumentTagDocumentFactory documentTagStoreFactory,
        EventStreamS3Settings s3Settings,
        EventStreamDefaultTypeSettings typeSettings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(s3Settings);
        ArgumentNullException.ThrowIfNull(documentTagStoreFactory);
        ArgumentNullException.ThrowIfNull(typeSettings);

        this.clientFactory = clientFactory;
        this.s3Settings = s3Settings;
        this.documentTagStoreFactory = documentTagStoreFactory;
        this.typeSettings = typeSettings;
    }

    /// <summary>
    /// Creates a new document in S3 with initialized stream metadata if missing; returns the materialized document.
    /// </summary>
    /// <param name="name">The object name used to determine the key prefix.</param>
    /// <param name="objectId">The identifier of the object to create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The created or existing object document loaded from storage.</returns>
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public async Task<IObjectDocument> CreateAsync(
        string name,
        string objectId,
        string? store = null)
    {
        var documentPath = $"{name}/{objectId}.json";
        var targetStore = store ?? s3Settings.DefaultDocumentStore;
        var bucketName = s3Settings.DefaultDocumentContainerName.ToLowerInvariant();
        var s3Client = clientFactory.CreateClient(targetStore);

        if (s3Settings.AutoCreateBucket)
        {
            await s3Client.EnsureBucketAsync(bucketName);
        }

        try
        {
            var exists = await s3Client.ObjectExistsAsync(bucketName, documentPath);
            if (!exists)
            {
                // Create new document using SerializeS3EventStreamDocument to exclude legacy properties
                var newDocument = new SerializeS3EventStreamDocument
                {
                    ObjectId = objectId,
                    ObjectName = name,
                    Active = new SerializeS3StreamInformation
                    {
                        StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
                        // Use type settings instead of hardcoded values
                        StreamType = typeSettings.StreamType,
                        DocumentType = typeSettings.DocumentType,
                        DocumentTagType = typeSettings.DocumentTagType,
                        EventStreamTagType = typeSettings.EventStreamTagType,
                        DocumentRefType = typeSettings.DocumentRefType,
                        CurrentStreamVersion = -1,
                        // Initialize store settings from the target store and S3 settings (new *Store properties only)
                        DocumentStore = targetStore,
                        DataStore = targetStore,
                        DocumentTagStore = s3Settings.DefaultDocumentTagStore,
                        StreamTagStore = s3Settings.DefaultDocumentTagStore,
                        SnapShotStore = s3Settings.DefaultSnapShotStore,
                        ChunkSettings = s3Settings.EnableStreamChunks
                            ? new StreamChunkSettings
                            {
                                EnableChunks = s3Settings.EnableStreamChunks,
                                ChunkSize = s3Settings.DefaultChunkSize,
                            }
                            : null,
                        StreamChunks = s3Settings.EnableStreamChunks
                            ?
                            [
                                new(chunkIdentifier: 0, firstEventVersion: 0, lastEventVersion: -1)
                            ]
                            : []
                    }
                };
                await s3Client.PutObjectAsEntityAsync(
                    bucketName,
                    documentPath,
                    newDocument,
                    SerializeS3EventStreamDocumentContext.Default.SerializeS3EventStreamDocument);
            }
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            throw new InvalidOperationException(
                $"The bucket '{bucketName}' was not found. " +
                "Please create it or adjust the config setting for DefaultDocumentContainerName.",
                ex);
        }

        // Download the document to return it
        var downloadResult = await s3Client.GetObjectAsEntityAsync(
            bucketName,
            documentPath,
            DeserializeS3EventStreamDocumentContext.Default.DeserializeS3EventStreamDocument);

        if (downloadResult.Document == null)
        {
            return null!;
        }

        var newDoc = downloadResult.Document.ToS3EventStreamDocument();
        newDoc.SetHash(downloadResult.Hash!, downloadResult.Hash!);
        return newDoc;
    }

    /// <summary>
    /// Retrieves and materializes the object document from S3 using the configured serializers.
    /// </summary>
    /// <param name="name">The object name used to determine the key prefix.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the document cannot be found.</exception>
    public async Task<IObjectDocument> GetAsync(
        string name,
        string objectId,
        string? store = null)
    {
        var documentPath = $"{name}/{objectId}.json";
        var targetStore = store ?? s3Settings.DefaultDocumentStore;
        var bucketName = s3Settings.DefaultDocumentContainerName.ToLowerInvariant();
        var s3Client = clientFactory.CreateClient(targetStore);

        string? etag;
        try
        {
            etag = await s3Client.GetObjectETagAsync(bucketName, documentPath);
            if (etag == null)
            {
                throw new AmazonS3Exception("Object not found")
                {
                    ErrorCode = "NoSuchKey",
                    StatusCode = HttpStatusCode.NotFound
                };
            }
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            throw new InvalidOperationException(
                $"The bucket '{bucketName}' was not found. " +
                "Please create it or adjust the config setting for DefaultDocumentContainerName.",
                ex);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            throw new InvalidOperationException(
                $"The object document for object '{name}' by the id '{objectId}' was not found in store '{s3Settings.DefaultDocumentStore}'. " +
                "Please create it or adjust the config setting for DefaultDocumentContainerName.",
                ex);
        }

        // Download content with same ETag
        var downloadResult = await s3Client.GetObjectAsEntityAsync(
            bucketName,
            documentPath,
            DeserializeS3EventStreamDocumentContext.Default.DeserializeS3EventStreamDocument,
            etag);

        if (downloadResult.Document == null)
        {
            return null!;
        }

        var newDoc = downloadResult.Document.ToS3EventStreamDocument();
        newDoc.SetHash(downloadResult.Hash!, downloadResult.Hash!);
        newDoc.DocumentPath = documentPath;
        return newDoc;
    }

    /// <summary>
    /// Retrieves the first document matching the given document tag from the tag store and loads it from S3 storage.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the document. If not provided, uses the default document store.</param>
    /// <returns>The first matching document or null if no document matches.</returns>
    public async Task<IObjectDocument?> GetFirstByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null)
    {
        var targetDocumentTagStore = documentTagStore ?? this.s3Settings.DefaultDocumentTagStore;
        var documentTagStoreInstance = documentTagStoreFactory.CreateDocumentTagStore(targetDocumentTagStore);
        var objectId = (await documentTagStoreInstance.GetAsync(objectName, tag)).FirstOrDefault();
        if (!string.IsNullOrEmpty(objectId))
        {
            return await GetAsync(objectName, objectId, store);
        }
        return null;
    }

    /// <summary>
    /// Retrieves all documents matching the given document tag from the tag store and loads them from S3 storage.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="tag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the documents. If not provided, uses the default document store.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null)
    {
        var targetDocumentTagStore = documentTagStore ?? this.s3Settings.DefaultDocumentTagStore;
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
    /// Persists the provided object document JSON to S3 storage, updating its hash for optimistic concurrency.
    /// </summary>
    /// <param name="document">The document to persist.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SetAsync(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var documentPath = $"{document.ObjectName}/{document.ObjectId}.json";

        // Use document-specific store if configured, otherwise fall back to default
        var documentStore = GetDocumentStoreName(document);
        var bucketName = s3Settings.DefaultDocumentContainerName.ToLowerInvariant();
        var s3Client = clientFactory.CreateClient(documentStore);

        if (s3Settings.AutoCreateBucket)
        {
            await s3Client.EnsureBucketAsync(bucketName);
        }

        // Use SerializeS3EventStreamDocument to exclude legacy properties
        var serializeDoc = SerializeS3EventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(serializeDoc);

        // Try to get ETag, but handle the case where object doesn't exist yet
        string? etag = null;
        try
        {
            etag = await s3Client.GetObjectETagAsync(bucketName, documentPath);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            // Object doesn't exist yet - this is fine for new documents being created with custom store settings
            etag = null;
        }

        var (_, hash) = await s3Client.PutObjectAsEntityAsync(
            bucketName,
            documentPath,
            serializeDoc,
            SerializeS3EventStreamDocumentContext.Default.SerializeS3EventStreamDocument,
            etag);

        document.SetHash(hash, document.Hash);
    }

    /// <summary>
    /// Gets the document store name from the document's active stream, falling back to the default if not configured.
    /// </summary>
    /// <param name="document">The document to retrieve the store name from.</param>
    /// <returns>The configured store name or the default document store.</returns>
    private string GetDocumentStoreName(IObjectDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.Active.DocumentStore))
        {
            return document.Active.DocumentStore;
        }

        return s3Settings.DefaultDocumentStore;
    }
}
