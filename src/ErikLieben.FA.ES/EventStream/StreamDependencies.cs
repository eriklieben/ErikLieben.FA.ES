using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Provides a concrete container for stream dependencies used when constructing event streams.
/// </summary>
public class StreamDependencies : IStreamDependencies
{
    /// <summary>
    /// Gets the data store used to persist and read events.
    /// </summary>
    public required IDataStore DataStore { get; init; }

    /// <summary>
    /// Gets the snapshot store used to persist and retrieve aggregate snapshots.
    /// </summary>
    public required ISnapShotStore SnapshotStore { get; init; }

    /// <summary>
    /// Gets the factory used to resolve object documents for streams.
    /// </summary>
    public required IObjectDocumentFactory ObjectDocumentFactory { get; init; }

    /// <summary>
    /// Gets the factory used to create aggregate instances.
    /// </summary>
    public required IAggregateFactory AggregateFactory { get; init; }
}
