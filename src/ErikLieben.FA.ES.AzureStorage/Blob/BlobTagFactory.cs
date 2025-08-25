using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public class BlobTagFactory : IDocumentTagDocumentFactory
{

    private readonly EventStreamDefaultTypeSettings settings;
    private readonly EventStreamBlobSettings blobSettings;
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;

    public BlobTagFactory(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        EventStreamDefaultTypeSettings settings,
        EventStreamBlobSettings blobSettings)
    {
        this.settings = settings;
        this.blobSettings = blobSettings;
        this.clientFactory = clientFactory;
    }

    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);
        return new BlobDocumentTagStore(clientFactory, settings.DocumentTagType, blobSettings.DefaultDocumentTagStore, blobSettings.AutoCreateContainer);
    }

    public IDocumentTagStore CreateDocumentTagStore()
    {
        return new BlobDocumentTagStore(clientFactory, settings.DocumentTagType, blobSettings.DefaultDocumentTagStore, blobSettings.AutoCreateContainer);
    }

    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        return new BlobDocumentTagStore(clientFactory, type, blobSettings.DefaultDocumentTagStore, blobSettings.AutoCreateContainer);
    }

    public IDocumentTagStore CreateStreamTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);
        return new BlobDocumentTagStore(clientFactory, settings.EventStreamTagType, blobSettings.DefaultDocumentTagStore, blobSettings.AutoCreateContainer);
    }
}
