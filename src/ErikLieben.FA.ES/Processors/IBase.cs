namespace ErikLieben.FA.ES.Processors;

/// <summary>
/// Base interface for event processors that apply events to rebuild state.
/// </summary>
public interface IBase
{
    /// <summary>
    /// Asynchronously folds (applies) all events from the stream to rebuild state.
    /// </summary>
    /// <returns>A task representing the asynchronous fold operation.</returns>
    Task Fold();

    /// <summary>
    /// Applies a single event to update the current state.
    /// </summary>
    /// <param name="event">The event to apply.</param>
    void Fold(IEvent @event);

    /// <summary>
    /// Applies a snapshot to restore state to a specific point.
    /// </summary>
    /// <param name="snapshot">The snapshot data to apply.</param>
    void ProcessSnapshot(object snapshot);
}
