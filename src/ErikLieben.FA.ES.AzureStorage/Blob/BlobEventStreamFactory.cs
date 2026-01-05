using Azure.Storage.Blobs;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Creates Azure Blob Storage-backed event streams for object documents.
/// </summary>
public class BlobEventStreamFactory : IEventStreamFactory
{
    private readonly EventStreamBlobSettings settings;
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IAggregateFactory aggregateFactory;

    /// <summary>
/// Initializes a new instance of the <see cref="BlobEventStreamFactory"/> class.
/// </summary>
/// <param name="settings">The Blob storage settings controlling default stores and behaviors.</param>
/// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
/// <param name="documentTagFactory">The factory used to create document tag stores.</param>
/// <param name="objectDocumentFactory">The object document factory used to resolve documents.</param>
/// <param name="aggregateFactory">The aggregate factory used to create aggregates for streams.</param>
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

    /// <summary>
    /// Creates an event stream for the specified document using Azure Blob Storage for data and snapshots.
    /// </summary>
    /// <param name="document">The object document the stream belongs to.</param>
    /// <returns>A new <see cref="IEventStream"/> instance configured for Blob storage.</returns>
    public IEventStream Create(IObjectDocument document)
    {
        if (document.Active.StreamType == "default")
        {
            document.Active.StreamType = settings.DefaultDataStore;
        }

        var documentTagStore = documentTagFactory.CreateDocumentTagStore(document);
        var streamTagStore = documentTagFactory.CreateStreamTagStore(document);

        return new BlobEventStream(
            new ObjectDocumentWithTags(document, documentTagStore, streamTagStore),
            new StreamDependencies
            {
                AggregateFactory = aggregateFactory,
                DataStore = new BlobDataStore(clientFactory, settings.AutoCreateContainer),
                SnapshotStore = new BlobSnapShotStore(clientFactory, settings),
                ObjectDocumentFactory = objectDocumentFactory,
            });
    }
}
