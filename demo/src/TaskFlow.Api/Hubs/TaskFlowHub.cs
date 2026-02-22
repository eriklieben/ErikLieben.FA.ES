using Microsoft.AspNetCore.SignalR;

namespace TaskFlow.Api.Hubs;

/// <summary>
/// SignalR hub for real-time updates to TaskFlow clients
/// </summary>
public class TaskFlowHub : Hub
{
    /// <summary>
    /// Join a project room to receive updates for that project
    /// </summary>
    public async Task JoinProject(string projectId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }

    /// <summary>
    /// Leave a project room to stop receiving updates
    /// </summary>
    public async Task LeaveProject(string projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        // Could track connected users here
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        // Could clean up user presence here
    }
}

/// <summary>
/// Extension methods for broadcasting events via SignalR
/// </summary>
public static class TaskFlowHubExtensions
{
    /// <summary>
    /// Broadcast that a work item was planned
    /// </summary>
    public static async Task BroadcastWorkItemPlanned(
        this IHubContext<TaskFlowHub> hubContext,
        string projectId,
        object workItemDto)
    {
        await hubContext.Clients
            .Group($"project-{projectId}")
            .SendAsync("WorkItemPlanned", workItemDto);
    }

    /// <summary>
    /// Broadcast that a work item changed
    /// </summary>
    public static async Task BroadcastWorkItemChanged(
        this IHubContext<TaskFlowHub> hubContext,
        string projectId,
        object workItemDto)
    {
        await hubContext.Clients
            .Group($"project-{projectId}")
            .SendAsync("WorkItemChanged", workItemDto);
    }

    /// <summary>
    /// Broadcast that work was completed
    /// </summary>
    public static async Task BroadcastWorkCompleted(
        this IHubContext<TaskFlowHub> hubContext,
        string projectId,
        object workItemDto)
    {
        await hubContext.Clients
            .Group($"project-{projectId}")
            .SendAsync("WorkCompleted", workItemDto);
    }

    /// <summary>
    /// Broadcast that a projection advanced
    /// </summary>
    public static async Task BroadcastProjectionAdvanced(
        this IHubContext<TaskFlowHub> hubContext,
        string projectionName,
        string checkpoint)
    {
        await hubContext.Clients
            .All
            .SendAsync("ProjectionAdvanced", new { projectionName, checkpoint });
    }

    /// <summary>
    /// Broadcast that an event occurred
    /// </summary>
    public static async Task BroadcastEventOccurred(
        this IHubContext<TaskFlowHub> hubContext,
        string projectId,
        string aggregateId,
        string eventType)
    {
        await hubContext.Clients
            .Group($"project-{projectId}")
            .SendAsync("EventOccurred", new { aggregateId, eventType });
    }

    /// <summary>
    /// Broadcast seed progress for demo data generation
    /// </summary>
    /// <param name="hubContext">The hub context</param>
    /// <param name="provider">The storage provider (blob, table, cosmos)</param>
    /// <param name="current">Current item number</param>
    /// <param name="total">Total items to create</param>
    /// <param name="message">Optional status message</param>
    public static async Task BroadcastSeedProgress(
        this IHubContext<TaskFlowHub> hubContext,
        string provider,
        int current,
        int total,
        string? message = null)
    {
        await hubContext.Clients
            .All
            .SendAsync("SeedProgress", new
            {
                provider,
                current,
                total,
                percentage = total > 0 ? (int)Math.Round((double)current / total * 100) : 0,
                message,
                timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Broadcast that a live migration has started
    /// </summary>
    public static async Task BroadcastLiveMigrationStarted(
        this IHubContext<TaskFlowHub> hubContext,
        string migrationId,
        string sourceStreamId,
        string targetStreamId,
        int sourceEventCount)
    {
        await hubContext.Clients
            .All
            .SendAsync("LiveMigrationStarted", new
            {
                migrationId,
                sourceStreamId,
                targetStreamId,
                sourceEventCount,
                timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Broadcast per-iteration progress during live migration catch-up
    /// </summary>
    public static async Task BroadcastLiveMigrationIterationProgress(
        this IHubContext<TaskFlowHub> hubContext,
        string migrationId,
        string phase,
        int iteration,
        int sourceVersion,
        int targetVersion,
        int eventsBehind,
        int eventsCopiedThisIteration,
        long totalEventsCopied,
        string elapsedTime,
        bool isSynced,
        string? message = null)
    {
        await hubContext.Clients
            .All
            .SendAsync("LiveMigrationIterationProgress", new
            {
                migrationId,
                phase,
                iteration,
                sourceVersion,
                targetVersion,
                eventsBehind,
                eventsCopiedThisIteration,
                totalEventsCopied,
                percentage = sourceVersion > 0 ? (int)Math.Round((double)targetVersion / sourceVersion * 100) : 0,
                elapsedTime,
                isSynced,
                message,
                timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Broadcast per-event progress during live migration (individual event copied)
    /// </summary>
    public static async Task BroadcastLiveMigrationEventCopied(
        this IHubContext<TaskFlowHub> hubContext,
        string migrationId,
        int eventVersion,
        string eventType,
        bool wasTransformed,
        string? originalEventType,
        int? originalSchemaVersion,
        int? newSchemaVersion,
        long totalEventsCopied,
        int sourceVersion)
    {
        await hubContext.Clients
            .All
            .SendAsync("LiveMigrationEventCopied", new
            {
                migrationId,
                eventVersion,
                eventType,
                wasTransformed,
                originalEventType,
                originalSchemaVersion,
                newSchemaVersion,
                totalEventsCopied,
                sourceVersion,
                percentage = sourceVersion > 0 ? (int)Math.Round((double)totalEventsCopied / sourceVersion * 100) : 0,
                timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Broadcast that a live migration has completed successfully
    /// </summary>
    public static async Task BroadcastLiveMigrationCompleted(
        this IHubContext<TaskFlowHub> hubContext,
        string migrationId,
        long totalEventsCopied,
        int iterations,
        string elapsedTime,
        int eventsTransformed)
    {
        await hubContext.Clients
            .All
            .SendAsync("LiveMigrationCompleted", new
            {
                migrationId,
                totalEventsCopied,
                iterations,
                elapsedTime,
                eventsTransformed,
                timestamp = DateTimeOffset.UtcNow
            });
    }

    /// <summary>
    /// Broadcast that a live migration has failed
    /// </summary>
    public static async Task BroadcastLiveMigrationFailed(
        this IHubContext<TaskFlowHub> hubContext,
        string migrationId,
        string error,
        int iterations,
        long eventsCopiedBeforeFailure)
    {
        await hubContext.Clients
            .All
            .SendAsync("LiveMigrationFailed", new
            {
                migrationId,
                error,
                iterations,
                eventsCopiedBeforeFailure,
                timestamp = DateTimeOffset.UtcNow
            });
    }
}
