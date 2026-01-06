namespace TaskFlow.Domain.Messaging;

/// <summary>
/// Handles a specific type of projection event.
/// </summary>
/// <typeparam name="TEvent">The type of event this handler processes.</typeparam>
public interface IProjectionEventHandler<in TEvent>
    where TEvent : IProjectionEvent
{
    /// <summary>
    /// Handles the specified event.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
