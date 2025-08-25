using Azure.Storage.Blobs;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public class BlobEventStreamFactory : IEventStreamFactory
{
    private readonly EventStreamBlobSettings settings;
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IAggregateFactory aggregateFactory;

    public BlobEventStreamFactory(
        EventStreamBlobSettings settings,
        IAzureClientFactory<BlobServiceClient> clientFactory,
        IDocumentTagDocumentFactory documentTagFactory,
        IObjectDocumentFactory objectDocumentFactory,
        IAggregateFactory aggregateFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(documentTagFactory);
        ArgumentNullException.ThrowIfNull(objectDocumentFactory);
        ArgumentNullException.ThrowIfNull(aggregateFactory);

        this.settings = settings;
        this.clientFactory = clientFactory;
        this.documentTagFactory = documentTagFactory;
        this.objectDocumentFactory = objectDocumentFactory;
        this.aggregateFactory = aggregateFactory;
    }

    public IEventStream Create(IObjectDocument document)
    {
        if (document.Active.StreamType == "default")
        {
            document.Active.StreamType = settings.DefaultDataStore;
        }

        var documentTagStore = documentTagFactory.CreateDocumentTagStore(document);

        return new BlobEventStream(
            new ObjectDocumentWithTags(document, documentTagStore),
            new StreamDependencies
            {
                AggregateFactory = aggregateFactory,
                DataStore = new BlobDataStore(clientFactory, settings.AutoCreateContainer),
                SnapshotStore = new BlobSnapShotStore(clientFactory, settings),
                ObjectDocumentFactory = objectDocumentFactory,
            });
    }
}
