using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Provides a Blob Storage-backed store for associating tags with event streams.
/// </summary>
public class BlobStreamTagStore : IDocumentTagStore
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly bool autoCreateContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStreamTagStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="defaultConnectionName">The default connection name (currently unused).</param>
    /// <param name="autoCreateContainer">True to create containers automatically when missing.</param>
    public BlobStreamTagStore(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        string defaultConnectionName,
        bool autoCreateContainer)
    {
        this.clientFactory = clientFactory;
        this.autoCreateContainer = autoCreateContainer;
    }

    /// <summary>
    /// Associates the specified tag with the stream of the given document.
    /// </summary>
    /// <param name="document">The document whose stream is tagged.</param>
    /// <param name="tag">The tag value to associate.</param>
    /// <returns>A task that represents the asynchronous tagging operation.</returns>
    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var blobDoc = BlobEventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(blobDoc);

        string? documentPath = $"tags/stream/{document.Active.StreamIdentifier}.json";
        var blob = CreateBlobClient(document, documentPath);

        if (!await blob.ExistsAsync())
        {
            var newDoc = new BlobDocumentTagStoreDocument
            {
                Tag = tag,
                ObjectIds = [ document.ObjectId ]
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

        if (!doc.ObjectIds.Any(d => d == document.ObjectId))
        {
            doc.ObjectIds.Add(document.ObjectId);
        }
        await blob.SaveEntityAsync(doc,
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag });
    }

    /// <summary>
    /// Gets the identifiers of streams that have the specified tag.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of stream identifiers; not yet implemented.</returns>
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
