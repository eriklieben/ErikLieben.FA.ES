using TaskFlow.Domain.Messaging;

namespace TaskFlow.Api.Messaging;

/// <summary>
/// Simple in-memory event dispatcher that routes events to registered handlers.
/// </summary>
public class ProjectionEventDispatcher : IProjectionEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectionEventDispatcher> _logger;

    public ProjectionEventDispatcher(
        IServiceProvider serviceProvider,
        ILogger<ProjectionEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IProjectionEvent
    {
        var handlers = _serviceProvider.GetServices<IProjectionEventHandler<TEvent>>();

        var handlersList = handlers.ToList();
        if (handlersList.Count == 0)
        {
            _logger.LogWarning(
                "No handlers registered for event type {EventType}",
                typeof(TEvent).Name);
            return;
        }

        _logger.LogDebug(
            "Publishing event {EventType} to {HandlerCount} handlers",
            typeof(TEvent).Name,
            handlersList.Count);

        // Execute all handlers in parallel
        var tasks = handlersList.Select(handler =>
            handler.HandleAsync(@event, cancellationToken));

        await Task.WhenAll(tasks);
    }
}
