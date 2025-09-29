using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemoryStream(
    IObjectDocumentWithMethods document,
    InMemoryDataStore inMemoryDataSource,
    ISnapShotStore snapShotStore,
    IObjectDocumentFactory objectDocumentFactory,
    IAggregateFactory aggregateFactory) : BaseEventStream(
    document,
    new StreamDependencies
    {
        AggregateFactory = aggregateFactory,
        DataStore = inMemoryDataSource,
        SnapshotStore = snapShotStore,
        ObjectDocumentFactory = objectDocumentFactory,
    })
{

    public IEnumerable<IEvent> Events
    {
        get
        {
            return inMemoryDataSource.Store[InMemoryDataStore.GetStoreKey(Document.ObjectName, Document.ObjectId)]
                .Select(e => e.Value);
        }
    }
}
