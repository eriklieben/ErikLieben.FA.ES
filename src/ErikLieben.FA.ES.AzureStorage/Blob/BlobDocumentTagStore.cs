using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public partial class BlobDocumentTagStore : IDocumentTagStore
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly bool autoCreateContainer;
    private readonly string defaultConnectionName;
    private readonly string defaultDocumentTagType;

    public BlobDocumentTagStore(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        string defaultDocumentTagType,
        string defaultConnectionName,
        bool autoCreateContainer)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);

        this.clientFactory = clientFactory;
        this.autoCreateContainer = autoCreateContainer;
        this.defaultDocumentTagType = defaultDocumentTagType;
        this.defaultConnectionName = defaultConnectionName;
    }

    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);
        //var blobDoc = BlobEventStreamDocument.From(document);

        var filename = ValidBlobFilenameRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var documentPath = $"tags/document/{filename}.json";
        var blob = CreateBlobClient(document, documentPath);

        if (!await blob.ExistsAsync())
        {
            var newDoc = new BlobDocumentTagStoreDocument
            {
                Tag = tag,
                ObjectIds = [document.ObjectId]
            };
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
        if (doc.ObjectIds.All(d => d != document.ObjectId))
        {
            doc.ObjectIds.Add(document.ObjectId);
        }

        await blob.SaveEntityAsync(doc,
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag });
    }

    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        var filename = ValidBlobFilenameRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var documentPath = $"tags/document/{filename}.json";

        var client = clientFactory.CreateClient(defaultConnectionName);
        var container = client.GetBlobContainerClient(objectName.ToLowerInvariant());
        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");

        var (doc, hash) = await blob.AsEntityAsync(BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument);
        if (doc == null)
        {
            // A bit more friendly than throwing an exception
            return [];
            //throw new BlobDataStoreProcessingException($"Unable to find tag document '{objectName.ToLowerInvariant()}/{documentPath}' while processing save.");
        }

        return doc.ObjectIds;
    }

    private BlobClient CreateBlobClient(IObjectDocument objectDocument, string documentPath)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(objectDocument.ObjectName);

        var client = clientFactory.CreateClient(objectDocument.Active.DocumentTagConnectionName);
        var container = client.GetBlobContainerClient(objectDocument.ObjectName.ToLowerInvariant());

        if (autoCreateContainer)
        {
            container.CreateIfNotExists();
        }

        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");
        return blob;
    }

    [GeneratedRegex(@"[\\\/*?<>|""\r\n]")]
    private static partial Regex ValidBlobFilenameRegex();
}
