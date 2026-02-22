using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.S3.Configuration;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Creates S3-backed event streams for object documents.
/// </summary>
public class S3EventStreamFactory : IEventStreamFactory
{
    private readonly EventStreamS3Settings settings;
    private readonly IS3ClientFactory clientFactory;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IAggregateFactory aggregateFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3EventStreamFactory"/> class.
    /// </summary>
    /// <param name="settings">The S3 storage settings controlling default stores and behaviors.</param>
    /// <param name="clientFactory">The S3 client factory used to create <see cref="Amazon.S3.IAmazonS3"/> instances.</param>
    /// <param name="documentTagFactory">The factory used to create document tag stores.</param>
    /// <param name="objectDocumentFactory">The object document factory used to resolve documents.</param>
    /// <param name="aggregateFactory">The aggregate factory used to create aggregates for streams.</param>
    public S3EventStreamFactory(
        EventStreamS3Settings settings,
        IS3ClientFactory clientFactory,
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
    /// Creates an event stream for the specified document using S3 for data and snapshots.
    /// </summary>
    /// <param name="document">The object document the stream belongs to.</param>
    /// <returns>A new <see cref="IEventStream"/> instance configured for S3 storage.</returns>
    public IEventStream Create(IObjectDocument document)
    {
        if (document.Active.StreamType == "default")
        {
            document.Active.StreamType = settings.DefaultDataStore;
        }

        var documentTagStore = documentTagFactory.CreateDocumentTagStore(document);
        var streamTagStore = documentTagFactory.CreateStreamTagStore(document);

        return new S3EventStream(
            new ObjectDocumentWithTags(document, documentTagStore, streamTagStore),
            new StreamDependencies
            {
                AggregateFactory = aggregateFactory,
                DataStore = new S3DataStore(clientFactory, settings),
                SnapshotStore = new S3SnapShotStore(clientFactory, settings),
                ObjectDocumentFactory = objectDocumentFactory,
            });
    }
}
