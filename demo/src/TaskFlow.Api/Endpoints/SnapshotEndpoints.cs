using Microsoft.AspNetCore.Mvc;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;
using System.Diagnostics;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Endpoints for demonstrating snapshot functionality.
/// Snapshots are point-in-time captures of aggregate state that improve load performance.
/// </summary>
public static class SnapshotEndpoints
{
    public static RouteGroupBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/snapshots")
            .WithTags("Snapshots")
            .WithDescription("Endpoints for managing and demonstrating aggregate snapshots");

        // Create snapshot for a work item
        group.MapPost("/workitems/{id}", CreateWorkItemSnapshot)
            .WithName("CreateWorkItemSnapshot")
            .WithSummary("Create a snapshot of a work item at its current version")
            .WithDescription("Creates a point-in-time snapshot of the work item state. " +
                           "Snapshots improve performance by allowing the system to load " +
                           "from the snapshot instead of replaying all events.");

        // Create snapshot at specific version
        group.MapPost("/workitems/{id}/version/{version:int}", CreateWorkItemSnapshotAtVersion)
            .WithName("CreateWorkItemSnapshotAtVersion")
            .WithSummary("Create a snapshot at a specific version")
            .WithDescription("Creates a snapshot capturing the work item state at a specific event version.");

        // Get snapshot info
        group.MapGet("/workitems/{id}/info", GetWorkItemSnapshotInfo)
            .WithName("GetWorkItemSnapshotInfo")
            .WithSummary("Get snapshot information for a work item")
            .WithDescription("Returns information about existing snapshots for the work item, " +
                           "including version numbers and metadata.");

        // Compare load performance
        group.MapGet("/workitems/{id}/benchmark", BenchmarkWorkItemLoad)
            .WithName("BenchmarkWorkItemLoad")
            .WithSummary("Benchmark load performance with and without snapshot")
            .WithDescription("Loads the work item with and without using snapshots to demonstrate " +
                           "the performance benefits. Results show event counts and load times.");

        // List all work items with snapshots
        group.MapGet("/workitems", ListWorkItemsWithSnapshots)
            .WithName("ListWorkItemsWithSnapshots")
            .WithSummary("List work items that have snapshots")
            .WithDescription("Returns a list of work items that have snapshot points available.");

        return group;
    }

    /// <summary>
    /// Creates a snapshot of a work item at its current version.
    /// </summary>
    private static async Task<IResult> CreateWorkItemSnapshot(
        string id,
        [FromServices] IWorkItemFactory workItemFactory)
    {
        try
        {
            // Load the work item
            var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));

            if (workItem.Metadata?.Id == null)
            {
                return Results.NotFound(new { error = "Work item not found", id });
            }

            var currentVersion = workItem.Metadata?.VersionInStream ?? 0;

            // Create the snapshot
            await workItem.CreateSnapshotAsync();

            return Results.Ok(new
            {
                success = true,
                message = "Snapshot created successfully",
                workItemId = id,
                snapshotVersion = currentVersion,
                workItemTitle = workItem.Title,
                timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to create snapshot",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    /// <summary>
    /// Creates a snapshot at a specific version.
    /// </summary>
    private static async Task<IResult> CreateWorkItemSnapshotAtVersion(
        string id,
        int version,
        [FromServices] IWorkItemFactory workItemFactory)
    {
        try
        {
            // Load the work item
            var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));

            if (workItem.Metadata?.Id == null)
            {
                return Results.NotFound(new { error = "Work item not found", id });
            }

            var currentVersion = workItem.Metadata?.VersionInStream ?? 0;
            if (version > currentVersion)
            {
                return Results.BadRequest(new
                {
                    error = "Invalid version",
                    message = $"Requested version {version} exceeds current version {currentVersion}",
                    currentVersion
                });
            }

            // Create the snapshot at the specified version
            await workItem.CreateSnapshotAsync(version);

            return Results.Ok(new
            {
                success = true,
                message = $"Snapshot created at version {version}",
                workItemId = id,
                snapshotVersion = version,
                currentVersion,
                workItemTitle = workItem.Title,
                timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to create snapshot",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    /// <summary>
    /// Gets snapshot information for a work item.
    /// </summary>
    private static async Task<IResult> GetWorkItemSnapshotInfo(
        string id,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IWorkItemFactory workItemFactory)
    {
        try
        {
            // Get the object document to access snapshot metadata
            var document = await objectDocumentFactory.GetAsync("workItem", id);

            if (document == null)
            {
                return Results.NotFound(new { error = "Work item not found", id });
            }

            // Also load the work item to get current state info
            var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));

            var snapshots = document.Active.SnapShots?.Select(s => new
            {
                version = s.UntilVersion,
                name = s.Name ?? "(default)"
            }).ToList() ?? [];

            return Results.Ok(new
            {
                workItemId = id,
                title = workItem?.Title,
                currentVersion = workItem?.Metadata?.VersionInStream ?? document.Active.CurrentStreamVersion,
                totalEvents = document.Active.CurrentStreamVersion + 1,
                hasSnapshots = snapshots.Count > 0,
                snapshotCount = snapshots.Count,
                snapshots,
                lastSnapshot = snapshots.LastOrDefault(),
                eventsAfterLastSnapshot = snapshots.Count > 0
                    ? (document.Active.CurrentStreamVersion - snapshots.Last().version)
                    : document.Active.CurrentStreamVersion + 1,
                recommendation = GetSnapshotRecommendation(document.Active.CurrentStreamVersion, snapshots.Count)
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get snapshot info",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    /// <summary>
    /// Benchmarks work item load performance with and without snapshots.
    /// </summary>
    private static async Task<IResult> BenchmarkWorkItemLoad(
        string id,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEventStreamFactory eventStreamFactory,
        [FromServices] IWorkItemFactory workItemFactory)
    {
        try
        {
            // Get the object document
            var document = await objectDocumentFactory.GetAsync("workItem", id);

            if (document == null)
            {
                return Results.NotFound(new { error = "Work item not found", id });
            }

            var hasSnapshots = document.Active.HasSnapShots();
            var totalEvents = document.Active.CurrentStreamVersion + 1;

            // Benchmark: Load from event stream only (full replay)
            var fullReplayStopwatch = Stopwatch.StartNew();

            // Read all events directly
            var stream = eventStreamFactory.Create(document);
            WorkItem.InitializeStream(stream);
            var allEvents = await stream.ReadAsync();
            var eventCount = allEvents.Count;

            // Fold all events manually (simulating full replay)
            var tempWorkItem = new WorkItem(stream);
            foreach (var e in allEvents)
            {
                tempWorkItem.Fold(e);
            }
            fullReplayStopwatch.Stop();
            var fullReplayTime = fullReplayStopwatch.ElapsedMilliseconds;

            // Benchmark: Load using factory (which uses snapshots if available)
            var withSnapshotStopwatch = Stopwatch.StartNew();
            var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));
            withSnapshotStopwatch.Stop();
            var withSnapshotTime = withSnapshotStopwatch.ElapsedMilliseconds;

            // Calculate events replayed after snapshot
            var eventsAfterSnapshot = hasSnapshots
                ? totalEvents - (document.Active.SnapShots?.LastOrDefault()?.UntilVersion ?? 0) - 1
                : totalEvents;

            return Results.Ok(new
            {
                workItemId = id,
                title = workItem?.Title,
                currentVersion = workItem?.Metadata?.VersionInStream,
                benchmark = new
                {
                    totalEvents,
                    hasSnapshots,
                    lastSnapshotVersion = hasSnapshots
                        ? document.Active.SnapShots?.LastOrDefault()?.UntilVersion
                        : null,
                    eventsReplayedWithSnapshot = eventsAfterSnapshot,
                    eventsReplayedWithoutSnapshot = eventCount,
                    performance = new
                    {
                        fullReplayMs = fullReplayTime,
                        withSnapshotMs = withSnapshotTime,
                        speedupFactor = fullReplayTime > 0 && withSnapshotTime > 0
                            ? Math.Round((double)fullReplayTime / withSnapshotTime, 2)
                            : 1.0,
                        timeSavedMs = fullReplayTime - withSnapshotTime
                    }
                },
                analysis = GetPerformanceAnalysis(totalEvents, hasSnapshots, fullReplayTime, withSnapshotTime)
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to benchmark",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    /// <summary>
    /// Lists work items that have snapshots.
    /// </summary>
    private static async Task<IResult> ListWorkItemsWithSnapshots(
        [FromServices] IObjectIdProvider objectIdProvider,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? continuationToken = null)
    {
        try
        {
            // Get all work item object IDs
            var result = await objectIdProvider.GetObjectIdsAsync("workItem", continuationToken, pageSize);

            var workItemsWithSnapshots = new List<object>();
            var workItemsWithoutSnapshots = 0;

            foreach (var objectId in result.Items)
            {
                var document = await objectDocumentFactory.GetAsync("workItem", objectId);
                if (document == null) continue;

                if (document.Active.HasSnapShots())
                {
                    workItemsWithSnapshots.Add(new
                    {
                        id = objectId,
                        totalEvents = document.Active.CurrentStreamVersion + 1,
                        snapshotCount = document.Active.SnapShots?.Count ?? 0,
                        lastSnapshotVersion = document.Active.SnapShots?.LastOrDefault()?.UntilVersion
                    });
                }
                else
                {
                    workItemsWithoutSnapshots++;
                }
            }

            return Results.Ok(new
            {
                itemsChecked = result.Items.Count,
                withSnapshots = workItemsWithSnapshots.Count,
                withoutSnapshots = workItemsWithoutSnapshots,
                workItems = workItemsWithSnapshots,
                hasMore = result.ContinuationToken != null,
                continuationToken = result.ContinuationToken
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to list work items",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static string GetSnapshotRecommendation(int currentVersion, int snapshotCount)
    {
        var eventCount = currentVersion + 1;

        if (eventCount < 50 && snapshotCount == 0)
            return "No snapshot needed yet. Consider creating one after 50+ events.";

        if (eventCount >= 100 && snapshotCount == 0)
            return "Recommended: Create a snapshot to improve load performance.";

        if (snapshotCount > 0)
        {
            if (eventCount > 200)
                return "Consider creating a new snapshot to capture recent events.";
            return "Snapshot exists. Load performance is optimized.";
        }

        return "Monitor event count and create snapshot when needed.";
    }

    private static object GetPerformanceAnalysis(int totalEvents, bool hasSnapshots, long fullReplayMs, long withSnapshotMs)
    {
        if (totalEvents < 10)
        {
            return new
            {
                summary = "Few events - snapshots provide minimal benefit",
                recommendation = "No action needed"
            };
        }

        if (!hasSnapshots && totalEvents > 50)
        {
            return new
            {
                summary = "Many events without snapshot - performance could be improved",
                recommendation = "Create a snapshot to reduce load time"
            };
        }

        if (hasSnapshots)
        {
            var improvement = fullReplayMs > 0 ? ((fullReplayMs - withSnapshotMs) * 100.0 / fullReplayMs) : 0;
            return new
            {
                summary = $"Snapshot provides ~{Math.Round(improvement)}% performance improvement",
                recommendation = totalEvents > 200
                    ? "Consider creating a fresh snapshot"
                    : "Current snapshot is effective"
            };
        }

        return new
        {
            summary = "Performance is adequate",
            recommendation = "Monitor as event count grows"
        };
    }
}
