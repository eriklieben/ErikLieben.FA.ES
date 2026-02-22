using TaskFlow.Domain.Messaging;

namespace TaskFlow.Api.Messaging;

/// <summary>
/// Example handler that logs projection update events.
/// Demonstrates how easy it is to add additional handlers with the mediator pattern.
/// </summary>
public class LoggingProjectionEventHandler : IProjectionEventHandler<ProjectionUpdateRequested>
{
    private readonly ILogger<LoggingProjectionEventHandler> logger;

    public LoggingProjectionEventHandler(ILogger<LoggingProjectionEventHandler> logger)
    {
        this.logger = logger;
    }

    public Task HandleAsync(ProjectionUpdateRequested @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Projection update requested: {ObjectName}/{VersionToken} with {EventCount} events. Targets: {Targets}",
            @event.ObjectName,
            @event.VersionToken.Value,
            @event.EventCount,
            @event.TargetProjections != null ? string.Join(", ", @event.TargetProjections) : "all");

        return Task.CompletedTask;
    }
}
