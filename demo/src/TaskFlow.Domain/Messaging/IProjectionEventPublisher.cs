namespace TaskFlow.Domain.Messaging;

/// <summary>
/// Publishes projection events to registered handlers.
/// </summary>
public interface IProjectionEventPublisher
{
    /// <summary>
    /// Publishes an event to all registered handlers.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IProjectionEvent;
}
