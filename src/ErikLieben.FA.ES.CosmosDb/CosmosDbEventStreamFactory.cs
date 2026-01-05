using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Creates CosmosDB-backed event streams for object documents.
/// </summary>
public class CosmosDbEventStreamFactory : IEventStreamFactory
{
    private readonly EventStreamCosmosDbSettings settings;
    private readonly CosmosClient cosmosClient;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IAggregateFactory aggregateFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbEventStreamFactory"/> class.
    /// </summary>
    /// <param name="settings">The CosmosDB settings controlling default stores and behaviors.</param>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="documentTagFactory">The factory used to create document tag stores.</param>
    /// <param name="objectDocumentFactory">The object document factory used to resolve documents.</param>
    /// <param name="aggregateFactory">The aggregate factory used to create aggregates for streams.</param>
    public CosmosDbEventStreamFactory(
        EventStreamCosmosDbSettings settings,
        CosmosClient cosmosClient,
        IDocumentTagDocumentFactory documentTagFactory,
        IObjectDocumentFactory objectDocumentFactory,
        IAggregateFactory aggregateFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(documentTagFactory);
        ArgumentNullException.ThrowIfNull(objectDocumentFactory);
        ArgumentNullException.ThrowIfNull(aggregateFactory);

        this.settings = settings;
        this.cosmosClient = cosmosClient;
        this.documentTagFactory = documentTagFactory;
        this.objectDocumentFactory = objectDocumentFactory;
        this.aggregateFactory = aggregateFactory;
    }

    /// <summary>
    /// Creates an event stream for the specified document using CosmosDB for data and snapshots.
    /// </summary>
    /// <param name="document">The object document the stream belongs to.</param>
    /// <returns>A new <see cref="IEventStream"/> instance configured for CosmosDB storage.</returns>
    public IEventStream Create(IObjectDocument document)
    {
        if (document.Active.StreamType == "default")
        {
            document.Active.StreamType = settings.DefaultDataStore;
        }

        var documentTagStore = documentTagFactory.CreateDocumentTagStore(document);
        var streamTagStore = documentTagFactory.CreateStreamTagStore(document);

        return new CosmosDbEventStream(
            new ObjectDocumentWithTags(document, documentTagStore, streamTagStore),
            new StreamDependencies
            {
                AggregateFactory = aggregateFactory,
                DataStore = new CosmosDbDataStore(cosmosClient, settings),
                SnapshotStore = new CosmosDbSnapShotStore(cosmosClient, settings),
                ObjectDocumentFactory = objectDocumentFactory,
            });
    }
}
