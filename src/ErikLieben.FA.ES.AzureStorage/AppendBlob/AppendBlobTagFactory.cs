using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob;

/// <summary>
/// Creates Blob Storage-backed document and stream tag stores for the Append Blob provider.
/// </summary>
public class AppendBlobTagFactory : IDocumentTagDocumentFactory
{
    private readonly EventStreamDefaultTypeSettings settings;
    private readonly EventStreamAppendBlobSettings appendBlobSettings;
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppendBlobTagFactory"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="settings">The default type settings used to resolve tag store types.</param>
    /// <param name="appendBlobSettings">The Append Blob storage settings controlling default stores and auto-creation.</param>
    public AppendBlobTagFactory(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        EventStreamDefaultTypeSettings settings,
        EventStreamAppendBlobSettings appendBlobSettings)
    {
        this.settings = settings;
        this.appendBlobSettings = appendBlobSettings;
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
        return new AppendBlobDocumentTagStore(clientFactory, settings.DocumentTagType, appendBlobSettings.DefaultDocumentTagStore, appendBlobSettings.AutoCreateContainer);
    }

    /// <summary>
    /// Creates a document tag store using the default document tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by Blob Storage.</returns>
    public IDocumentTagStore CreateDocumentTagStore()
    {
        return new AppendBlobDocumentTagStore(clientFactory, settings.DocumentTagType, appendBlobSettings.DefaultDocumentTagStore, appendBlobSettings.AutoCreateContainer);
    }

    /// <summary>
    /// Creates a document tag store for the specified tag provider type.
    /// </summary>
    /// <param name="type">The tag provider type (e.g., "appendblob").</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by the specified provider.</returns>
    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        return new AppendBlobDocumentTagStore(clientFactory, type, appendBlobSettings.DefaultDocumentTagStore, appendBlobSettings.AutoCreateContainer);
    }

    /// <summary>
    /// Creates a stream tag store for the specified document using the configured stream tag provider type.
    /// </summary>
    /// <param name="document">The document whose stream tag store is requested.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> for stream tags.</returns>
    public IDocumentTagStore CreateStreamTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.EventStreamTagType);
        return new AppendBlobStreamTagStore(clientFactory, appendBlobSettings.DefaultDocumentTagStore, appendBlobSettings.AutoCreateContainer);
    }

    /// <summary>
    /// Creates a stream tag store using the default stream tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> for stream tags.</returns>
    public IDocumentTagStore CreateStreamTagStore()
    {
        return new AppendBlobStreamTagStore(clientFactory, appendBlobSettings.DefaultDocumentTagStore, appendBlobSettings.AutoCreateContainer);
    }
}
