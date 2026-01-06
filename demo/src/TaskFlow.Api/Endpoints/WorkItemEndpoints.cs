using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Api.DTOs;
using TaskFlow.Api.Helpers;
using TaskFlow.Api.Hubs;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Events;
using ErikLieben.FA.ES.Aggregates;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.Endpoints;

public static class WorkItemEndpoints
{
    public static RouteGroupBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/workitems")
            .WithTags("Work Items")
            .WithDescription("Work item management endpoints for planning, assigning, and tracking tasks");

        group.MapPost("/", PlanWorkItem)
            .WithName("PlanWorkItem")
            .WithSummary("Plan a new work item");

        group.MapPut("/{id}/assign", AssignResponsibility)
            .WithName("AssignResponsibility")
            .WithSummary("Assign responsibility for a work item");

        group.MapPut("/{id}/unassign", RelinquishResponsibility)
            .WithName("RelinquishResponsibility")
            .WithSummary("Relinquish responsibility for a work item");

        group.MapPost("/{id}/commence", CommenceWork)
            .WithName("CommenceWork")
            .WithSummary("Commence work on a work item");

        group.MapPost("/{id}/complete", CompleteWork)
            .WithName("CompleteWork")
            .WithSummary("Complete a work item");

        group.MapPost("/{id}/revive", ReviveWorkItem)
            .WithName("ReviveWorkItem")
            .WithSummary("Revive a completed work item");

        group.MapPut("/{id}/priority", Reprioritize)
            .WithName("Reprioritize")
            .WithSummary("Change the priority of a work item");

        group.MapPut("/{id}/estimate", ReestimateEffort)
            .WithName("ReestimateEffort")
            .WithSummary("Update the effort estimate");

        group.MapPut("/{id}/requirements", RefineRequirements)
            .WithName("RefineRequirements")
            .WithSummary("Refine work item requirements/description");

        group.MapPost("/{id}/feedback", ProvideFeedback)
            .WithName("ProvideFeedback")
            .WithSummary("Add feedback/comment to a work item");

        group.MapPut("/{id}/relocate", RelocateWorkItem)
            .WithName("RelocateWorkItem")
            .WithSummary("Move work item to a different project");

        group.MapPut("/{id}/tags", Retag)
            .WithName("Retag")
            .WithSummary("Update work item tags");

        group.MapPut("/{id}/deadline", EstablishDeadline)
            .WithName("EstablishDeadline")
            .WithSummary("Set or update the deadline");

        group.MapDelete("/{id}/deadline", RemoveDeadline)
            .WithName("RemoveDeadline")
            .WithSummary("Remove the deadline");

        group.MapPost("/{id}/move-back-to-inprogress", MoveBackToInProgress)
            .WithName("MoveBackToInProgress")
            .WithSummary("Move work item backward from Completed to InProgress");

        group.MapPost("/{id}/move-back-to-planned-from-completed", MoveBackToPlannedFromCompleted)
            .WithName("MoveBackToPlannedFromCompleted")
            .WithSummary("Move work item backward from Completed to Planned");

        group.MapPost("/{id}/move-back-to-planned-from-inprogress", MoveBackToPlannedFromInProgress)
            .WithName("MoveBackToPlannedFromInProgress")
            .WithSummary("Move work item backward from InProgress to Planned");

        group.MapPost("/{id}/mark-drag-accidental", MarkDragAccidental)
            .WithName("MarkDragAccidental")
            .WithSummary("Mark a drag operation as accidental");

        group.MapGet("/{id}", GetWorkItem)
            .WithName("GetWorkItem")
            .WithSummary("Get work item details");

        group.MapGet("/", ListWorkItems)
            .WithName("ListWorkItems")
            .WithSummary("List work items (optionally filtered by project)");

        return group;
    }

    private static async Task<IResult> PlanWorkItem(
        [FromBody] PlanWorkItemRequest request,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromServices] IProjectFactory projectFactory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        // Use the factory extension method to create and link in one operation
        var (result, workItem) = await workItemFactory.CreateAndLinkToProjectAsync(
            ProjectId.From(request.ProjectId),
            request.Title,
            request.Description,
            request.Priority,
            userId,
            projectFactory,
            userToken);

        if (!result.IsSuccess)
        {
            return Results.BadRequest(new
            {
                errors = result.Errors.ToArray().Select(e => new
                {
                    property = e.PropertyName,
                    message = e.Message
                })
            });
        }

        var workItemId = workItem!.Metadata.Id;

        var dto = new WorkItemDto(
            workItemId!.Value.ToString(),
            request.ProjectId,
            request.Title,
            request.Description,
            request.Priority,
            WorkItemStatus.Planned,
            null,
            null,
            null,
            [],
            0,
            0);

        await hubContext.BroadcastWorkItemPlanned(request.ProjectId, dto);

        return Results.Created($"/api/workitems/{workItemId.Value}",
            new CommandResult(true, "Work item planned successfully", workItemId.Value.ToString()));
    }

    private static async Task<IResult> AssignResponsibility(
        string id,
        [FromBody] AssignResponsibilityRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.AssignResponsibility(request.MemberId, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            assignedTo = request.MemberId
        });

        return Results.Ok(new CommandResult(true, "Responsibility assigned successfully"));
    }

    private static async Task<IResult> RelinquishResponsibility(
        string id,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.RelinquishResponsibility(userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            assignedTo = (string?)null
        });

        return Results.Ok(new CommandResult(true, "Responsibility relinquished successfully"));
    }

    private static async Task<IResult> CommenceWork(
        string id,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromServices] IProjectFactory projectFactory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));
        var previousStatus = workItem.Status;

        var result = await workItem.CommenceWork(userId, userToken);

        if (!result.IsSuccess)
        {
            return Results.BadRequest(new
            {
                errors = result.Errors.ToArray().Select(e => new
                {
                    property = e.PropertyName,
                    message = e.Message
                })
            });
        }

        // Update work item status in project (move from previous status to InProgress)
        if (workItem.ProjectId != null)
        {
            var project = await projectFactory.GetAsync(ProjectId.From(workItem.ProjectId));
            await project.UpdateWorkItemStatus(
                WorkItemId.From(id),
                previousStatus,
                WorkItemStatus.InProgress,
                userId,
                userToken);
        }

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            status = WorkItemStatus.InProgress
        });

        return Results.Ok(new CommandResult(true, "Work commenced successfully"));
    }

    private static async Task<IResult> CompleteWork(
        string id,
        [FromBody] CompleteWorkRequest request,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromServices] IProjectFactory projectFactory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));
        var previousStatus = workItem.Status;

        var result = await workItem.CompleteWork(request.Outcome, userId, userToken);

        if (!result.IsSuccess)
        {
            return Results.BadRequest(new
            {
                errors = result.Errors.ToArray().Select(e => new
                {
                    property = e.PropertyName,
                    message = e.Message
                })
            });
        }

        // Update work item status in project (move from previous status to Completed)
        if (workItem.ProjectId != null)
        {
            var project = await projectFactory.GetAsync(ProjectId.From(workItem.ProjectId));
            await project.UpdateWorkItemStatus(
                WorkItemId.From(id),
                previousStatus,
                WorkItemStatus.Completed,
                userId,
                userToken);
        }

        await hubContext.BroadcastWorkCompleted(workItem.ProjectId!, new
        {
            workItemId = id,
            status = WorkItemStatus.Completed
        });

        return Results.Ok(new CommandResult(true, "Work completed successfully"));
    }

    private static async Task<IResult> ReviveWorkItem(
        string id,
        [FromBody] ReviveWorkItemRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.ReviveTask(request.Rationale, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            status = WorkItemStatus.Planned
        });

        return Results.Ok(new CommandResult(true, "Work item revived successfully"));
    }

    private static async Task<IResult> Reprioritize(
        string id,
        [FromBody] ReprioritizeRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.Reprioritize(request.NewPriority, request.Rationale, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            priority = request.NewPriority
        });

        return Results.Ok(new CommandResult(true, "Work item reprioritized successfully"));
    }

    private static async Task<IResult> ReestimateEffort(
        string id,
        [FromBody] ReestimateEffortRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.ReestimateEffort(request.EstimatedHours, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            estimatedHours = request.EstimatedHours
        });

        return Results.Ok(new CommandResult(true, "Effort reestimated successfully"));
    }

    private static async Task<IResult> RefineRequirements(
        string id,
        [FromBody] RefineRequirementsRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.RefineRequirements(request.NewDescription, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            description = request.NewDescription
        });

        return Results.Ok(new CommandResult(true, "Requirements refined successfully"));
    }

    private static async Task<IResult> ProvideFeedback(
        string id,
        [FromBody] ProvideFeedbackRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.ProvideFeedback(request.Content, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            feedbackAdded = true
        });

        return Results.Ok(new CommandResult(true, "Feedback provided successfully"));
    }

    private static async Task<IResult> RelocateWorkItem(
        string id,
        [FromBody] RelocateWorkItemRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        var oldProjectId = workItem.ProjectId;

        await workItem.RelocateToProject(request.NewProjectId, request.Rationale, userId, userToken);

        // Notify both old and new project rooms
        if (oldProjectId != null)
        {
            await hubContext.Clients.Group($"project-{oldProjectId}")
                .SendAsync("WorkItemRelocated", new { workItemId = id, moved = "out" });
        }

        await hubContext.Clients.Group($"project-{request.NewProjectId}")
            .SendAsync("WorkItemRelocated", new { workItemId = id, moved = "in" });

        return Results.Ok(new CommandResult(true, "Work item relocated successfully"));
    }

    private static async Task<IResult> Retag(
        string id,
        [FromBody] RetagRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.Retag(request.Tags, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            tags = request.Tags
        });

        return Results.Ok(new CommandResult(true, "Tags updated successfully"));
    }

    private static async Task<IResult> EstablishDeadline(
        string id,
        [FromBody] EstablishDeadlineRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.EstablishDeadline(request.Deadline, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            deadline = request.Deadline
        });

        return Results.Ok(new CommandResult(true, "Deadline established successfully"));
    }

    private static async Task<IResult> RemoveDeadline(
        string id,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.RemoveDeadline(userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            deadline = (DateTime?)null
        });

        return Results.Ok(new CommandResult(true, "Deadline removed successfully"));
    }

    private static async Task<IResult> MoveBackToInProgress(
        string id,
        [FromBody] MoveBackRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.MoveBackFromCompletedToInProgress(request.Reason, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            status = WorkItemStatus.InProgress,
            movedBackward = true
        });

        return Results.Ok(new CommandResult(true, "Work item moved back to In Progress"));
    }

    private static async Task<IResult> MoveBackToPlannedFromCompleted(
        string id,
        [FromBody] MoveBackRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.MoveBackFromCompletedToPlanned(request.Reason, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            status = WorkItemStatus.Planned,
            movedBackward = true
        });

        return Results.Ok(new CommandResult(true, "Work item moved back to Planned"));
    }

    private static async Task<IResult> MoveBackToPlannedFromInProgress(
        string id,
        [FromBody] MoveBackRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.MoveBackFromInProgressToPlanned(request.Reason, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            status = WorkItemStatus.Planned,
            movedBackward = true
        });

        return Results.Ok(new CommandResult(true, "Work item moved back to Planned"));
    }

    private static async Task<IResult> MarkDragAccidental(
        string id,
        [FromBody] MarkDragAccidentalRequest request,
        [FromServices] IWorkItemFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var userId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await factory.GetAsync(WorkItemId.From(id));
        await workItem.MarkDragAsAccidental(request.FromStatus, request.ToStatus, userId, userToken);

        await hubContext.BroadcastWorkItemChanged(workItem.ProjectId!, new
        {
            workItemId = id,
            dragMarkedAccidental = true
        });

        return Results.Ok(new CommandResult(true, "Drag marked as accidental"));
    }

    private static async Task<IResult> GetWorkItem(
        string id,
        [FromServices] IWorkItemFactory factory)
    {
        var workItem = await factory.GetAsync(WorkItemId.From(id));

        var dto = new WorkItemDto(
            id,
            workItem.ProjectId ?? "",
            workItem.Title ?? "",
            workItem.Description ?? "",
            workItem.Priority,
            workItem.Status,
            workItem.AssignedTo,
            workItem.Deadline,
            workItem.EstimatedHours,
            workItem.Tags.ToArray(),
            workItem.Comments.Count,
            0);

        return Results.Ok(dto);
    }

    private static async Task<IResult> ListWorkItems(
        [FromQuery] string? projectId,
        [FromServices] IAggregateFactory factory)
    {
        // For now, return empty list - we'll implement projection-based listing later
        await System.Threading.Tasks.Task.CompletedTask;
        return Results.Ok(Array.Empty<WorkItemListDto>());
    }
}
