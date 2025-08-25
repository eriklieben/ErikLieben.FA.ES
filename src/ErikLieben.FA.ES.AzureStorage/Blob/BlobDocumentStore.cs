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

public class BlobDocumentStore : IBlobDocumentStore
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly EventStreamBlobSettings blobSettings;
    private readonly EventStreamDefaultTypeSettings settings;
    private readonly IDocumentTagDocumentFactory documentTagStoreFactory;

    public BlobDocumentStore(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagStoreFactory,
        EventStreamBlobSettings blobSettings,
        EventStreamDefaultTypeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(blobSettings);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(documentTagStoreFactory);

        this.clientFactory = clientFactory;
        this.blobSettings = blobSettings;
        this.settings = settings;
        this.documentTagStoreFactory = documentTagStoreFactory;
    }

    public async Task<IObjectDocument> CreateAsync(
        string name,
        string objectId)
    {
        var documentPath = $"{name}/{objectId}.json";
        var blob = CreateBlobClient(blobSettings.DefaultDocumentStore, blobSettings.DefaultDocumentContainerName, documentPath);

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
                            StreamConnectionName = blobSettings.DefaultDocumentStore,
                            SnapShotConnectionName = blobSettings.DefaultSnapShotStore,
                            DocumentTagConnectionName = blobSettings.DefaultDocumentTagStore,
                            StreamTagConnectionName = blobSettings.DefaultDocumentTagStore,
                            StreamIdentifier = $"{objectId.Replace("-", string.Empty)}-0000000000",
                            StreamType = "blob",
                            DocumentTagType = "blob",
                            CurrentStreamVersion = -1,
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

        newDoc.SetHash(ComputeSha256Hash(json),ComputeSha256Hash(json));
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

    public async Task<IObjectDocument> GetAsync(
        string name,
        string objectId)
    {

        var documentPath = $"{name}/{objectId}.json";
        var blob = CreateBlobClient(blobSettings.DefaultDocumentStore, blobSettings.DefaultDocumentContainerName, documentPath);

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

    public async Task<IObjectDocument?> GetFirstByDocumentByTagAsync(string objectName, string tag)
    {
        var documentTagStore = documentTagStoreFactory.CreateDocumentTagStore(this.blobSettings.DefaultDocumentTagStore);
        var objectId = (await documentTagStore.GetAsync(objectName, tag)).ToList().FirstOrDefault();
        if (!string.IsNullOrEmpty(objectId))
        {
            return await GetAsync(objectName, objectId);
        }
        return null!;
    }


    public async Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(string objectName, string tag)
    {
        var documentTagStore = documentTagStoreFactory.CreateDocumentTagStore(this.settings.DocumentTagType);
        var objectIds = (await documentTagStore.GetAsync(objectName, tag)).ToList();
        var documents = new List<IObjectDocument>();
        foreach (var objectId in objectIds)
        {
            documents.Add(await GetAsync(objectName, objectId));
        }
        return documents;
    }

    public async Task SetAsync(IObjectDocument document)
    {
        var documentPath = $"{document.ObjectName}/{document.ObjectId}.json";
        var blob = CreateBlobClient(blobSettings.DefaultDocumentStore, blobSettings.DefaultDocumentContainerName, documentPath);

        var blobDoc = BlobEventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(blobDoc);

        var properties = await blob.GetPropertiesAsync();
        var etagRetrieved = properties.Value.ETag.ToString().Replace("\u0022", string.Empty);

        var (etag, hash) = await blob.SaveEntityAsync(blobDoc, BlobEventStreamDocumentContext.Default.BlobEventStreamDocument,
            new BlobRequestConditions { IfMatch = etagRetrieved != null ? new ETag(etagRetrieved) : null });

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
}
