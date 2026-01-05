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
/// Provides a Blob Storage-backed store for associating tags with event streams.
/// Tags are stored by tag name at <c>tags/stream-by-tag/{tag}.json</c>, containing a list of stream identifiers.
/// </summary>
public partial class BlobStreamTagStore : IDocumentTagStore
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly string defaultConnectionName;
    private readonly bool autoCreateContainer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStreamTagStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="defaultConnectionName">The default connection name used when building blob clients.</param>
    /// <param name="autoCreateContainer">True to create containers automatically when missing.</param>
    public BlobStreamTagStore(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        string defaultConnectionName,
        bool autoCreateContainer)
    {
        this.clientFactory = clientFactory;
        this.defaultConnectionName = defaultConnectionName;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var filename = ValidBlobFilenameRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var documentPath = $"tags/stream-by-tag/{filename}.json";
        var blob = CreateBlobClient(document, documentPath);

        if (!await blob.ExistsAsync())
        {
            var newDoc = new BlobDocumentTagStoreDocument
            {
                Tag = tag,
                ObjectIds = [document.Active.StreamIdentifier]
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

        var doc = (await blob.AsEntityAsync(
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag })).Item1
            ?? throw new BlobDataStoreProcessingException($"Unable to find tag document '{document.ObjectName.ToLowerInvariant()}/{documentPath}' while processing save.");

        if (doc.ObjectIds.All(d => d != document.Active.StreamIdentifier))
        {
            doc.ObjectIds.Add(document.Active.StreamIdentifier);
        }

        await blob.SaveEntityAsync(doc,
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag });
    }

    /// <summary>
    /// Gets the identifiers of streams that have the specified tag within the given object scope.
    /// </summary>
    /// <param name="objectName">The object name (container scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of stream identifiers; empty when the tag document does not exist.</returns>
    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var filename = ValidBlobFilenameRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var documentPath = $"tags/stream-by-tag/{filename}.json";

        var client = clientFactory.CreateClient(defaultConnectionName);
        var container = client.GetBlobContainerClient(objectName.ToLowerInvariant());
        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");

        var (doc, _) = await blob.AsEntityAsync(BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument);
        if (doc == null)
        {
            return [];
        }

        return doc.ObjectIds;
    }

    /// <summary>
    /// Removes the specified tag from the stream of the given document.
    /// </summary>
    /// <param name="document">The document whose stream tag should be removed.</param>
    /// <param name="tag">The tag value to remove.</param>
    /// <returns>A task that represents the asynchronous removal operation.</returns>
    public async Task RemoveAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var filename = ValidBlobFilenameRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var documentPath = $"tags/stream-by-tag/{filename}.json";
        var blob = CreateBlobClient(document, documentPath);

        if (!await blob.ExistsAsync())
        {
            // Tag doesn't exist, nothing to remove
            return;
        }

        var properties = await blob.GetPropertiesAsync();
        var etag = properties.Value.ETag;

        var (doc, _) = await blob.AsEntityAsync(
            BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
            new BlobRequestConditions { IfMatch = etag });

        if (doc == null)
        {
            return;
        }

        doc.ObjectIds.Remove(document.Active.StreamIdentifier);

        if (doc.ObjectIds.Count == 0)
        {
            // No more streams with this tag, delete the blob
            await blob.DeleteIfExistsAsync(conditions: new BlobRequestConditions { IfMatch = etag });
        }
        else
        {
            // Update the blob with the remaining streams
            await blob.SaveEntityAsync(doc,
                BlobDocumentTagStoreDocumentContext.Default.BlobDocumentTagStoreDocument,
                new BlobRequestConditions { IfMatch = etag });
        }
    }

    private BlobClient CreateBlobClient(IObjectDocument objectDocument, string documentPath)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(objectDocument.ObjectName);

        // Use DataStore, falling back to deprecated StreamConnectionName for backwards compatibility
#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.DataStore)
            ? objectDocument.Active.DataStore
            : objectDocument.Active.StreamConnectionName;
#pragma warning restore CS0618
        var client = clientFactory.CreateClient(connectionName);
        var container = client.GetBlobContainerClient(objectDocument.ObjectName.ToLowerInvariant());

        if (autoCreateContainer)
        {
            container.CreateIfNotExists();
        }

        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");
        return blob!;
    }

    [GeneratedRegex(@"[\\\/*?<>|""\r\n]")]
    private static partial Regex ValidBlobFilenameRegex();
}
