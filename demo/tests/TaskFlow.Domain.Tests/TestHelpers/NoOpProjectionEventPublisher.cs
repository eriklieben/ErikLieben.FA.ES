using TaskFlow.Domain.Messaging;

namespace TaskFlow.Domain.Tests.TestHelpers;

/// <summary>
/// No-op implementation of IProjectionEventPublisher for unit tests.
/// Simply accepts events without doing anything with them.
/// </summary>
public class NoOpProjectionEventPublisher : IProjectionEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IProjectionEvent
    {
        // No-op: unit tests don't need to actually publish projection events
        return Task.CompletedTask;
    }
}
