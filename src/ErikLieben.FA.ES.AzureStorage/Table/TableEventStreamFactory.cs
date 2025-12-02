using Azure.Data.Tables;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Creates Azure Table Storage-backed event streams for object documents.
/// </summary>
public class TableEventStreamFactory : IEventStreamFactory
{
    private readonly EventStreamTableSettings settings;
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IAggregateFactory aggregateFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableEventStreamFactory"/> class.
    /// </summary>
    /// <param name="settings">The Table storage settings controlling default stores and behaviors.</param>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
    /// <param name="documentTagFactory">The factory used to create document tag stores.</param>
    /// <param name="objectDocumentFactory">The object document factory used to resolve documents.</param>
    /// <param name="aggregateFactory">The aggregate factory used to create aggregates for streams.</param>
    public TableEventStreamFactory(
        EventStreamTableSettings settings,
        IAzureClientFactory<TableServiceClient> clientFactory,
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
    /// Creates an event stream for the specified document using Azure Table Storage for data and snapshots.
    /// </summary>
    /// <param name="document">The object document the stream belongs to.</param>
    /// <returns>A new <see cref="IEventStream"/> instance configured for Table storage.</returns>
    public IEventStream Create(IObjectDocument document)
    {
        if (document.Active.StreamType == "default")
        {
            document.Active.StreamType = settings.DefaultDataStore;
        }

        var documentTagStore = documentTagFactory.CreateDocumentTagStore(document);

        return new TableEventStream(
            new ObjectDocumentWithTags(document, documentTagStore),
            new StreamDependencies
            {
                AggregateFactory = aggregateFactory,
                DataStore = new TableDataStore(clientFactory, settings),
                SnapshotStore = new TableSnapShotStore(clientFactory, settings),
                ObjectDocumentFactory = objectDocumentFactory,
            });
    }
}
