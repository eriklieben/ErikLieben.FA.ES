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

/// <summary>
/// Provides an Azure Blob Storage-backed implementation of <see cref="IDocumentTagStore"/> for associating and querying document tags.
/// </summary>
public partial class BlobDocumentTagStore : IDocumentTagStore
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly bool autoCreateContainer;
    private readonly string defaultConnectionName;

    /// <summary>
/// Initializes a new instance of the <see cref="BlobDocumentTagStore"/> class.
/// </summary>
/// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
/// <param name="defaultDocumentTagType">The default tag provider type (e.g., "blob").</param>
/// <param name="defaultConnectionName">The default connection name used when building blob clients.</param>
/// <param name="autoCreateContainer">True to create containers automatically when missing.</param>
public BlobDocumentTagStore(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        string defaultDocumentTagType,
        string defaultConnectionName,
        bool autoCreateContainer)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);

        this.clientFactory = clientFactory;
        this.autoCreateContainer = autoCreateContainer;
        this.defaultConnectionName = defaultConnectionName;
    }

    /// <summary>
    /// Associates the specified tag with the given document by storing a tag document in Blob Storage.
    /// </summary>
    /// <param name="document">The document to tag.</param>
    /// <param name="tag">The tag value to associate with the document.</param>
    /// <returns>A task that represents the asynchronous tagging operation.</returns>
    /// <exception cref="BlobDataStoreProcessingException">Thrown when the tag document cannot be found during an update.</exception>
    /// <exception cref="DocumentConfigurationException">Thrown when the blob client cannot be created.</exception>
    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);
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

            try
            {
                await blob.SaveEntityAsync(
                    newDoc,
                    BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
                    new BlobRequestConditions { IfNoneMatch = new ETag("*") });
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Blob was created between ExistsAsync check and SaveEntityAsync call
                // Fall through to update logic below
            }
        }

        var properties = await blob.GetPropertiesAsync();
        var etag = properties.Value.ETag;

        // Download the document with the same tag, so that we're sure it's not overriden in the meantime
        var doc = (await blob.AsEntityAsync(
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag })).Item1
            ?? throw new BlobDataStoreProcessingException($"Unable to find tag document '{document.ObjectName.ToLowerInvariant()}/{documentPath}' while processing save.");

        if (doc.ObjectIds.All(d => d != document.ObjectId))
        {
            doc.ObjectIds.Add(document.ObjectId);
        }

        await blob.SaveEntityAsync(doc,
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag });
    }

    /// <summary>
    /// Gets the identifiers of documents that have the specified tag within the given object scope.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of document identifiers; empty when the tag document does not exist.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the blob client cannot be created.</exception>
    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        var filename = ValidBlobFilenameRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var documentPath = $"tags/document/{filename}.json";

        var client = clientFactory.CreateClient(defaultConnectionName);
        var container = client.GetBlobContainerClient(objectName.ToLowerInvariant());
        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");

        var (doc, _) = await blob.AsEntityAsync(BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument);
        if (doc == null)
        {
            // A bit more friendly than throwing an exception
            return [];
        }

        return doc.ObjectIds;
    }

    /// <summary>
    /// Creates a <see cref="BlobClient"/> for the given document and tag path, ensuring the container exists when configured.
    /// </summary>
    /// <param name="objectDocument">The object document that provides the container scope and connection name.</param>
    /// <param name="documentPath">The blob path of the tag document.</param>
    /// <returns>A <see cref="BlobClient"/> configured for the tag path.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the blob client cannot be created.</exception>
    private BlobClient CreateBlobClient(IObjectDocument objectDocument, string documentPath)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(objectDocument.ObjectName);

        // Use DocumentTagStore, falling back to deprecated DocumentTagConnectionName for backwards compatibility
#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.DocumentTagStore)
            ? objectDocument.Active.DocumentTagStore
            : objectDocument.Active.DocumentTagConnectionName;
#pragma warning restore CS0618
        var client = clientFactory.CreateClient(connectionName);
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
