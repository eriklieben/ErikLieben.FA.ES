using System.Diagnostics;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Azure;
using BlobDataStoreDocumentContext = ErikLieben.FA.ES.AzureStorage.Blob.Model.BlobDataStoreDocumentContext;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public class BlobDataStore : IDataStore
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly bool autoCreateContainer;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AzureStorage");

    public BlobDataStore(IAzureClientFactory<BlobServiceClient> clientFactory, bool autoCreateContainer)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);

        this.clientFactory = clientFactory;
        this.autoCreateContainer = autoCreateContainer;
    }

    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null)
    {
        using var activity = ActivitySource.StartActivity("BlobDataStore.ReadAsync");
        
        string? documentPath = null;
        if (document.Active.ChunkingEnabled())
        {
            documentPath = $"{document.Active.StreamIdentifier}-{chunk:d10}.json";
        }
        else
        {
            documentPath = $"{document.Active.StreamIdentifier}.json";
        }
        var blob = CreateBlobClient(document, documentPath);
        
        

        BlobDataStoreDocument? dataDocument = null;
        try
        {
            dataDocument = (await blob.AsEntityAsync(BlobDataStoreDocumentContext.Default.BlobDataStoreDocument)).Item1;
            if (dataDocument == null)
            {
                return null!;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "ContainerNotFound")
        {
            throw new BlobDocumentStoreContainerNotFoundException(
                $"The container by the name '{document.ObjectName?.ToLowerInvariant()}' is not found. " +
                "Create a container in your deployment or create it by hand, it's not created for you.",
            ex);
        }
        return dataDocument.Events
                    .Where(e => e.EventVersion >= startVersion && (!untilVersion.HasValue || e.EventVersion <= untilVersion))
                    .ToList();
    }

    public async Task AppendAsync(IObjectDocument document, params IEvent[] events)
    {
        using var activity = ActivitySource.StartActivity("BlobDataStore.AppendAsync");
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var blobDoc = BlobEventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(blobDoc);

        if (events.Length == 0)
        {
            throw new ArgumentException("No events provided to store.");
        }

        string? documentPath = null;
        if (document.Active.ChunkingEnabled())
        {
            var lastChunk = document.Active.StreamChunks.Last();
            documentPath = $"{document.Active.StreamIdentifier}-{lastChunk.ChunkIdentifier:d10}.json";
        }
        else
        {
            documentPath = $"{document.Active.StreamIdentifier}.json";
        }

        var blob = CreateBlobClient(document, documentPath);

        if (!await blob.ExistsAsync())
        {
            var newDoc = new BlobDataStoreDocument {
                ObjectId = document.ObjectId,
                ObjectName = document.ObjectName,
                LastObjectDocumentHash = blobDoc.Hash ?? "*"
            };
            newDoc.Events.AddRange(events.Select(BlobJsonEvent.From)!);
            newDoc.LastObjectDocumentHash = blobDoc.Hash ?? "*";
            await blob.SaveEntityAsync(
                newDoc,
                BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
                new BlobRequestConditions { IfNoneMatch = new ETag("*") });
            return;
        }

        var properties = await blob.GetPropertiesAsync();
        var etag = properties.Value.ETag;

        // Download the document with the same tag, so that we're sure it's not overriden in the meantime
        var doc = (await blob.AsEntityAsync(
            BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
            new BlobRequestConditions { IfMatch = etag })).Item1
            ?? throw new BlobDataStoreProcessingException($"Unable to find document '{document.ObjectName.ToLowerInvariant()}/{documentPath}' while processing save.");

        if (doc.LastObjectDocumentHash != "*" && doc.LastObjectDocumentHash != blobDoc.PrevHash)
        {
            throw new Exception("Something bad is going on");
        }
        doc.LastObjectDocumentHash = blobDoc.Hash ?? "*";

        doc.Events.AddRange(events.Select(BlobJsonEvent.From)!);
        await blob.SaveEntityAsync(doc,
            BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
            new BlobRequestConditions { IfMatch = etag });
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
