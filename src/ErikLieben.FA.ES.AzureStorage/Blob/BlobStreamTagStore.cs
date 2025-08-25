using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public class BlobStreamTagStore : IDocumentTagStore
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly bool autoCreateContainer;

    public BlobStreamTagStore(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        string defaultConnectionName,
        bool autoCreateContainer)
    {
        this.clientFactory = clientFactory;
        this.autoCreateContainer = autoCreateContainer;
    }

    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var blobDoc = BlobEventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(blobDoc);

        string? documentPath = $"tags/stream/{document.Active.StreamIdentifier}.json"; ;
        var blob = CreateBlobClient(document, documentPath);

        if (!await blob.ExistsAsync())
        {
            var newDoc = new BlobDocumentTagStoreDocument
            {
                Tag = tag,
                ObjectIds = [ document.ObjectId ]
            };
            // newDoc.Tags.Add(tag);
            // newDoc.LastObjectDocumentETag = blobDoc.Hash ?? "*";
            await blob.SaveEntityAsync(
                newDoc,
                BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
                new BlobRequestConditions { IfNoneMatch = new ETag("*") });
            return;
        }

        var properties = await blob.GetPropertiesAsync();
        var etag = properties.Value.ETag;

        // Download the document with the same tag, so that we're sure it's not overriden in the meantime
        var doc = (await blob.AsEntityAsync(
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag })).Item1
            ?? throw new BlobDataStoreProcessingException($"Unable to find tag document '{document.ObjectName.ToLowerInvariant()}/{documentPath}' while processing save.");

        // if (doc.LastObjectDocumentETag != "*" && doc.LastObjectDocumentETag != blobDoc.PrevHash)
        // {
        //     throw new Exception("Something bad is going on");
        // }
        // doc.LastObjectDocumentETag = blobDoc.Hash ?? "*";


        if (!doc.ObjectIds.Any(d => d == document.ObjectId))
        {
            doc.ObjectIds.Add(document.ObjectId);
        }
        await blob.SaveEntityAsync(doc,
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag });
    }

    public Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        throw new NotImplementedException();
    }

    private BlobClient CreateBlobClient(IObjectDocument objectDocument, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

        var client = clientFactory.CreateClient(objectDocument.Active.StreamConnectionName);
        var container = client.GetBlobContainerClient(objectDocument.ObjectName.ToLowerInvariant());

        if (autoCreateContainer)
        {
            container.CreateIfNotExists();
        }

        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");
        return blob!;
    }
}
