using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Postgres.Configuration;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Creates Postgres-backed event streams for object documents.
/// </summary>
public sealed class PostgresEventStreamFactory : IEventStreamFactory
{
    private readonly EventStreamPostgresSettings settings;
    private readonly PostgresDataStore dataStore;
    private readonly ISnapShotStore snapshotStore;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IAggregateFactory aggregateFactory;

    public PostgresEventStreamFactory(
        EventStreamPostgresSettings settings,
        PostgresDataStore dataStore,
        ISnapShotStore snapshotStore,
        IDocumentTagDocumentFactory documentTagFactory,
        IObjectDocumentFactory objectDocumentFactory,
        IAggregateFactory aggregateFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dataStore);
        ArgumentNullException.ThrowIfNull(snapshotStore);
        ArgumentNullException.ThrowIfNull(documentTagFactory);
        ArgumentNullException.ThrowIfNull(objectDocumentFactory);
        ArgumentNullException.ThrowIfNull(aggregateFactory);

        this.settings = settings;
        this.dataStore = dataStore;
        this.snapshotStore = snapshotStore;
        this.documentTagFactory = documentTagFactory;
        this.objectDocumentFactory = objectDocumentFactory;
        this.aggregateFactory = aggregateFactory;
    }

    public IEventStream Create(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Active.StreamType == "default")
        {
            document.Active.StreamType = settings.DefaultDataStore;
        }

        var documentTagStore = documentTagFactory.CreateDocumentTagStore(document);
        var streamTagStore = documentTagFactory.CreateStreamTagStore(document);

        return new PostgresEventStream(
            new ObjectDocumentWithTags(document, documentTagStore, streamTagStore),
            new StreamDependencies
            {
                AggregateFactory = aggregateFactory,
                DataStore = dataStore,
                SnapshotStore = snapshotStore,
                ObjectDocumentFactory = objectDocumentFactory,
            });
    }
}
