using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Functions.Functions;

/// <summary>
/// Sample Azure Functions demonstrating timer-triggered functions for scheduled tasks.
/// These functions show patterns for scheduled projection updates and maintenance.
/// </summary>
public class TimerFunctions(ILogger<TimerFunctions> logger)
{
    /// <summary>
    /// Scheduled projection refresh that runs every 5 minutes.
    /// Demonstrates using [ProjectionOutput] with timer triggers.
    /// </summary>
    /// <remarks>
    /// CRON expression: "0 */5 * * * *" = Every 5 minutes
    ///
    /// The [ProjectionOutput] attributes cause the projections to be updated
    /// after each timer execution, ensuring projections stay up-to-date.
    ///
    /// Note: In production, adjust the schedule based on your needs:
    /// - More frequent for real-time dashboards
    /// - Less frequent for batch reporting projections
    /// </remarks>
    [Function(nameof(ScheduledProjectionRefresh))]
    [ProjectionOutput<ActiveWorkItems>]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task ScheduledProjectionRefresh(
        [TimerTrigger("0 */5 * * * *", RunOnStartup = false)] TimerInfo timerInfo)
    {
        logger.LogInformation("Scheduled projection refresh triggered at {Time}", DateTime.UtcNow);

        if (timerInfo.IsPastDue)
        {
            logger.LogWarning("Timer is running late. Last execution was past due.");
        }

        // Log schedule info for debugging
        if (timerInfo.ScheduleStatus != null)
        {
            logger.LogDebug(
                "Next scheduled run: {NextRun}, Last run: {LastRun}",
                timerInfo.ScheduleStatus.Next,
                timerInfo.ScheduleStatus.Last);
        }

        // The actual projection updates happen in the middleware after this function returns
        // We just log that the refresh was triggered
        logger.LogInformation(
            "Projection refresh completed. Updated: {Projections}",
            $"{nameof(ActiveWorkItems)}, {nameof(ProjectKanbanBoard)}");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Daily maintenance task that runs at midnight UTC.
    /// Demonstrates longer-running scheduled tasks.
    /// </summary>
    /// <remarks>
    /// CRON expression: "0 0 0 * * *" = Every day at midnight UTC
    ///
    /// Use this pattern for:
    /// - Daily reports generation
    /// - Data cleanup tasks
    /// - Aggregation/rollup operations
    /// </remarks>
    [Function(nameof(DailyMaintenanceTask))]
    public async Task DailyMaintenanceTask(
        [TimerTrigger("0 0 0 * * *", RunOnStartup = false)] TimerInfo timerInfo)
    {
        logger.LogInformation("Daily maintenance task started at {Time}", DateTime.UtcNow);

        // Example maintenance tasks:
        // 1. Archive old completed work items
        // 2. Generate daily summary reports
        // 3. Clean up expired data
        // 4. Update statistics

        if (timerInfo.IsPastDue)
        {
            logger.LogWarning("Daily maintenance is running late");
        }

        // Simulate some maintenance work
        await Task.Delay(100);

        logger.LogInformation("Daily maintenance task completed");
    }

    /// <summary>
    /// Hourly health check that logs projection status.
    /// Demonstrates loading projections in timer functions without updating them.
    /// </summary>
    /// <remarks>
    /// CRON expression: "0 0 * * * *" = Every hour at minute 0
    /// </remarks>
    [Function(nameof(HourlyHealthCheck))]
    public async Task HourlyHealthCheck(
        [TimerTrigger("0 0 * * * *", RunOnStartup = false)] TimerInfo timerInfo,
        [ProjectionInput] ProjectKanbanBoard kanbanBoard,
        [ProjectionInput] ActiveWorkItems activeWorkItems)
    {
        logger.LogInformation("Hourly health check started at {Time}", DateTime.UtcNow);

        // Log projection status
        var projectCount = kanbanBoard.Projects?.Count ?? 0;
        var activeCount = activeWorkItems.WorkItems?.Count ?? 0;

        logger.LogInformation(
            "Projection status - Projects: {ProjectCount}, Active Work Items: {ActiveCount}",
            projectCount,
            activeCount);

        // Log checkpoint fingerprints for debugging sync issues
        logger.LogDebug(
            "Checkpoint fingerprints - Kanban: {KanbanFingerprint}, ActiveItems: {ActiveFingerprint}",
            kanbanBoard.CheckpointFingerprint,
            activeWorkItems.CheckpointFingerprint);

        // Check for potential issues
        if (projectCount == 0)
        {
            logger.LogWarning("No projects found in kanban board projection");
        }

        await Task.CompletedTask;
    }
}
