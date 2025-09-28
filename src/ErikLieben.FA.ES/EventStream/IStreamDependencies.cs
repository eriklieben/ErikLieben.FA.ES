using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Defines the required dependencies for constructing and operating event streams.
/// </summary>
public interface IStreamDependencies
{
    /// <summary>
    /// Gets the data store used to persist and read events.
    /// </summary>
    IDataStore DataStore { get; init; }

    /// <summary>
    /// Gets the snapshot store used to persist and retrieve aggregate snapshots.
    /// </summary>
    ISnapShotStore SnapshotStore { get; init; }

    /// <summary>
    /// Gets the factory used to resolve object documents for streams.
    /// </summary>
    IObjectDocumentFactory ObjectDocumentFactory { get; init; }

    /// <summary>
    /// Gets the factory used to create aggregate instances.
    /// </summary>
    IAggregateFactory AggregateFactory { get; init; }
}
