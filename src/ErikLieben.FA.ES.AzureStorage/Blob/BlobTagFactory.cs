using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Creates Blob Storage-backed document and stream tag stores.
/// </summary>
public class BlobTagFactory : IDocumentTagDocumentFactory
{

    private readonly EventStreamDefaultTypeSettings settings;
    private readonly EventStreamBlobSettings blobSettings;
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobTagFactory"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="settings">The default type settings used to resolve tag store types.</param>
    /// <param name="blobSettings">The Blob storage settings controlling default stores and auto-creation.</param>
    public BlobTagFactory(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        EventStreamDefaultTypeSettings settings,
        EventStreamBlobSettings blobSettings)
    {
        this.settings = settings;
        this.blobSettings = blobSettings;
        this.clientFactory = clientFactory;
    }

    /// <summary>
    /// Creates a document tag store for the specified object document using its configured tag type.
    /// </summary>
    /// <param name="document">The document whose tag configuration is used.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by Blob Storage.</returns>
    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);
        return new BlobDocumentTagStore(clientFactory, settings.DocumentTagType, blobSettings.DefaultDocumentTagStore, blobSettings.AutoCreateContainer);
    }

    /// <summary>
    /// Creates a document tag store using the default document tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by Blob Storage.</returns>
    public IDocumentTagStore CreateDocumentTagStore()
    {
        return new BlobDocumentTagStore(clientFactory, settings.DocumentTagType, blobSettings.DefaultDocumentTagStore, blobSettings.AutoCreateContainer);
    }

    /// <summary>
    /// Creates a document tag store for the specified tag provider type.
    /// </summary>
    /// <param name="type">The tag provider type (e.g., "blob").</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by the specified provider.</returns>
    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        return new BlobDocumentTagStore(clientFactory, type, blobSettings.DefaultDocumentTagStore, blobSettings.AutoCreateContainer);
    }

    /// <summary>
    /// Creates a stream tag store for the specified document using the configured stream tag provider type.
    /// </summary>
    /// <param name="document">The document whose stream tag store is requested.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> for stream tags.</returns>
    public IDocumentTagStore CreateStreamTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);
        return new BlobDocumentTagStore(clientFactory, settings.EventStreamTagType, blobSettings.DefaultDocumentTagStore, blobSettings.AutoCreateContainer);
    }
}
