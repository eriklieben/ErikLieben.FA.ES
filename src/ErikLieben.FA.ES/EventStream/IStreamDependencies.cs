using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.EventStream;

public interface IStreamDependencies
{
    IDataStore DataStore { get; init; }
    ISnapShotStore SnapshotStore { get; init; }
    IObjectDocumentFactory ObjectDocumentFactory { get; init; }
    IAggregateFactory AggregateFactory { get; init; }
}
