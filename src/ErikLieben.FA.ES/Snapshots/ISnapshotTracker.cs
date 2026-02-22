namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Tracks snapshot state for an aggregate, enabling automatic snapshot policy decisions.
/// </summary>
/// <remarks>
/// This interface is implemented by the <see cref="Processors.Aggregate"/> base class,
/// making snapshot tracking available to all aggregates automatically.
/// </remarks>
public interface ISnapshotTracker
{
    /// <summary>
    /// Gets the number of events appended since the last snapshot was created.
    /// </summary>
    int EventsSinceLastSnapshot { get; }

    /// <summary>
    /// Gets the total number of events processed by this aggregate instance.
    /// </summary>
    int TotalEventsProcessed { get; }

    /// <summary>
    /// Gets the version of the last snapshot, or null if no snapshot has been created/loaded.
    /// </summary>
    int? LastSnapshotVersion { get; }

    /// <summary>
    /// Records that events were appended to the stream.
    /// </summary>
    /// <param name="count">The number of events appended.</param>
    void RecordEventsAppended(int count);

    /// <summary>
    /// Records that events were folded (processed) by the aggregate.
    /// </summary>
    /// <param name="count">The number of events folded.</param>
    void RecordEventsFolded(int count);

    /// <summary>
    /// Records that a snapshot was created at the specified version.
    /// </summary>
    /// <param name="version">The version at which the snapshot was created.</param>
    void RecordSnapshotCreated(int version);

    /// <summary>
    /// Resets tracking state when loading from a snapshot.
    /// </summary>
    /// <param name="snapshotVersion">The version of the loaded snapshot.</param>
    void ResetFromSnapshot(int snapshotVersion);
}
