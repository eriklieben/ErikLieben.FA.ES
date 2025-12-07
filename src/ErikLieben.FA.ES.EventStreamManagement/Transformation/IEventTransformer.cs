namespace ErikLieben.FA.ES.EventStreamManagement.Transformation;

/// <summary>
/// Defines a contract for transforming events during migration (upcasting/versioning).
/// </summary>
public interface IEventTransformer
{
    /// <summary>
    /// Transforms a source event to a target format.
    /// </summary>
    /// <param name="sourceEvent">The event to transform.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transformed event, or the original if no transformation is needed.</returns>
    Task<IEvent> TransformAsync(IEvent sourceEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether this transformer can transform the given event.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="version">The version of the event.</param>
    /// <returns>True if the transformer can handle this event; otherwise false.</returns>
    bool CanTransform(string eventName, int version);
}
