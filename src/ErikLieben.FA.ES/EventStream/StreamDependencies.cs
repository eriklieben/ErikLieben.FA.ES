using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.EventStream;

public class StreamDependencies : IStreamDependencies
{
    public required IDataStore DataStore { get; init; }
    public required ISnapShotStore SnapshotStore { get; init; }
    public required IObjectDocumentFactory ObjectDocumentFactory { get; init; }
    public required IAggregateFactory AggregateFactory { get; init; }
}
