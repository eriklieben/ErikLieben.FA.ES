using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Api.DTOs;
using TaskFlow.Api.Helpers;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Projections;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.Endpoints;

public static class TimeSheetEndpoints
{
    public static RouteGroupBuilder MapTimeSheetEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/timesheets")
            .WithTags("TimeSheets")
            .WithDescription("Time tracking endpoints for logging hours against work items (Append Blob-backed)");

        group.MapPost("/", CreateTimeSheet)
            .WithName("CreateTimeSheet")
            .WithSummary("Create a new timesheet for a user and period");

        group.MapGet("/", ListTimeSheets)
            .WithName("ListTimeSheets")
            .WithSummary("List all timesheets");

        group.MapGet("/statistics", GetStatistics)
            .WithName("GetTimeSheetStatistics")
            .WithSummary("Get timesheet statistics");

        group.MapGet("/pending", GetPendingApproval)
            .WithName("GetPendingTimeSheets")
            .WithSummary("Get timesheets pending approval");

        group.MapGet("/user/{userId}", GetByUser)
            .WithName("GetTimeSheetsByUser")
            .WithSummary("Get timesheets for a specific user");

        group.MapGet("/project/{projectId}", GetByProject)
            .WithName("GetTimeSheetsByProject")
            .WithSummary("Get timesheets for a specific project");

        group.MapGet("/{id}", GetTimeSheet)
            .WithName("GetTimeSheet")
            .WithSummary("Get timesheet details");

        group.MapPost("/{id}/log", LogTime)
            .WithName("LogTime")
            .WithSummary("Log time against a work item");

        group.MapPut("/{id}/adjust", AdjustTime)
            .WithName("AdjustTime")
            .WithSummary("Adjust a time entry");

        group.MapPost("/{id}/entry/remove", RemoveEntry)
            .WithName("RemoveTimeEntry")
            .WithSummary("Remove a time entry");

        group.MapPost("/{id}/submit", SubmitTimeSheet)
            .WithName("SubmitTimeSheet")
            .WithSummary("Submit timesheet for approval");

        group.MapPost("/{id}/approve", ApproveTimeSheet)
            .WithName("ApproveTimeSheet")
            .WithSummary("Approve a submitted timesheet");

        group.MapPost("/{id}/reject", RejectTimeSheet)
            .WithName("RejectTimeSheet")
            .WithSummary("Reject a submitted timesheet");

        return group;
    }

    private static async Task<IResult> CreateTimeSheet(
        [FromBody] CreateTimeSheetRequest request,
        [FromServices] ITimeSheetFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var (_, initiatorToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var (result, timeSheet) = await factory.CreateTimeSheetAsync(
            UserProfileId.From(request.UserId),
            ProjectId.From(request.ProjectId),
            request.PeriodStart,
            request.PeriodEnd,
            initiatorToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("TimeSheetCreated", new
        {
            timeSheetId = timeSheet!.Metadata!.Id.Value.ToString(),
            userId = request.UserId,
            projectId = request.ProjectId
        });

        return Results.Created($"/api/timesheets/{timeSheet.Metadata.Id.Value}",
            new CommandResult(true, "Timesheet created successfully", timeSheet.Metadata.Id.Value.ToString()));
    }

    private static Task<IResult> ListTimeSheets(
        [FromServices] TimeSheetDashboard dashboard)
    {
        var sheets = dashboard.GetAllTimeSheets()
            .Select(ts => new TimeSheetListDto(
                ts.TimeSheetId,
                ts.UserId,
                ts.ProjectId,
                ts.PeriodStart,
                ts.PeriodEnd,
                ts.Status,
                ts.TotalHours,
                ts.CreatedAt))
            .ToList();

        return Task.FromResult(Results.Ok(sheets));
    }

    private static Task<IResult> GetStatistics(
        [FromServices] TimeSheetDashboard dashboard)
    {
        var stats = dashboard.GetStatistics();

        return Task.FromResult(Results.Ok(new TimeSheetStatisticsDto(
            stats.TotalTimeSheets,
            stats.OpenCount,
            stats.SubmittedCount,
            stats.ApprovedCount,
            stats.RejectedCount,
            stats.TotalHoursLogged,
            stats.TotalApprovedHours)));
    }

    private static Task<IResult> GetPendingApproval(
        [FromServices] TimeSheetDashboard dashboard)
    {
        var sheets = dashboard.GetPendingApproval()
            .Select(ts => new TimeSheetListDto(
                ts.TimeSheetId,
                ts.UserId,
                ts.ProjectId,
                ts.PeriodStart,
                ts.PeriodEnd,
                ts.Status,
                ts.TotalHours,
                ts.CreatedAt))
            .ToList();

        return Task.FromResult(Results.Ok(sheets));
    }

    private static Task<IResult> GetByUser(
        string userId,
        [FromServices] TimeSheetDashboard dashboard)
    {
        var sheets = dashboard.GetByUser(userId)
            .Select(ts => new TimeSheetListDto(
                ts.TimeSheetId,
                ts.UserId,
                ts.ProjectId,
                ts.PeriodStart,
                ts.PeriodEnd,
                ts.Status,
                ts.TotalHours,
                ts.CreatedAt))
            .ToList();

        return Task.FromResult(Results.Ok(sheets));
    }

    private static Task<IResult> GetByProject(
        string projectId,
        [FromServices] TimeSheetDashboard dashboard)
    {
        var sheets = dashboard.GetByProject(projectId)
            .Select(ts => new TimeSheetListDto(
                ts.TimeSheetId,
                ts.UserId,
                ts.ProjectId,
                ts.PeriodStart,
                ts.PeriodEnd,
                ts.Status,
                ts.TotalHours,
                ts.CreatedAt))
            .ToList();

        return Task.FromResult(Results.Ok(sheets));
    }

    private static Task<IResult> GetTimeSheet(
        string id,
        [FromServices] TimeSheetDashboard dashboard)
    {
        var sheet = dashboard.GetById(id);

        if (sheet == null)
        {
            return Task.FromResult(Results.NotFound(new CommandResult(false, "Timesheet not found")));
        }

        return Task.FromResult(Results.Ok(new TimeSheetDto(
            sheet.TimeSheetId,
            sheet.UserId,
            sheet.ProjectId,
            sheet.PeriodStart,
            sheet.PeriodEnd,
            sheet.Status,
            sheet.EntryCount,
            sheet.TotalHours,
            sheet.CreatedAt,
            sheet.SubmittedAt,
            sheet.ApprovedBy,
            sheet.ApprovedAt,
            sheet.RejectionReason)));
    }

    private static async Task<IResult> LogTime(
        string id,
        [FromBody] LogTimeRequest request,
        [FromServices] ITimeSheetFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var timeSheet = await factory.GetAsync(TimeSheetId.From(id));
        var (_, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await timeSheet.LogTime(
            WorkItemId.From(request.WorkItemId),
            request.Hours,
            request.Description,
            request.Date,
            userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("TimeLogged", new { timeSheetId = id, hours = request.Hours });

        return Results.Ok(new CommandResult(true, $"Logged {request.Hours}h successfully"));
    }

    private static async Task<IResult> AdjustTime(
        string id,
        [FromBody] AdjustTimeRequest request,
        [FromServices] ITimeSheetFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var timeSheet = await factory.GetAsync(TimeSheetId.From(id));
        var (_, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await timeSheet.AdjustTime(
            request.EntryId,
            request.NewHours,
            request.NewDescription,
            request.Reason,
            userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("TimeAdjusted", new { timeSheetId = id });

        return Results.Ok(new CommandResult(true, "Time entry adjusted"));
    }

    private static async Task<IResult> RemoveEntry(
        string id,
        [FromBody] RemoveTimeEntryRequest request,
        [FromServices] ITimeSheetFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var timeSheet = await factory.GetAsync(TimeSheetId.From(id));
        var (_, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await timeSheet.RemoveEntry(
            request.EntryId,
            request.Reason,
            userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("TimeEntryRemoved", new { timeSheetId = id });

        return Results.Ok(new CommandResult(true, "Time entry removed"));
    }

    private static async Task<IResult> SubmitTimeSheet(
        string id,
        [FromServices] ITimeSheetFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var timeSheet = await factory.GetAsync(TimeSheetId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await timeSheet.Submit(userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("TimeSheetSubmitted", new { timeSheetId = id });

        return Results.Ok(new CommandResult(true, "Timesheet submitted for approval"));
    }

    private static async Task<IResult> ApproveTimeSheet(
        string id,
        [FromServices] ITimeSheetFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var timeSheet = await factory.GetAsync(TimeSheetId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await timeSheet.Approve(userId, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("TimeSheetApproved", new { timeSheetId = id });

        return Results.Ok(new CommandResult(true, "Timesheet approved"));
    }

    private static async Task<IResult> RejectTimeSheet(
        string id,
        [FromBody] RejectTimeSheetRequest request,
        [FromServices] ITimeSheetFactory factory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        var timeSheet = await factory.GetAsync(TimeSheetId.From(id));
        var (userId, userToken) = await userProfileFactory.GetCurrentUserAsync(currentUserService);

        var result = await timeSheet.Reject(userId, request.Reason, userToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new CommandResult(false, string.Join(", ", result.Errors.ToArray().Select(e => e.Message))));
        }

        await hubContext.Clients.All.SendAsync("TimeSheetRejected", new { timeSheetId = id });

        return Results.Ok(new CommandResult(true, "Timesheet rejected"));
    }
}
