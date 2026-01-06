using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.DTOs;
using TaskFlow.Api.Helpers;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;
using ErikLieben.FA.ES.Aggregates;

namespace TaskFlow.Api.Endpoints;

public static class UserProfileEndpoints
{
    // Admin user with well-known GUID for demo purposes
    public const string ADMIN_USER_ID = "00000000-0000-0000-0000-000000000001";

    public static RouteGroupBuilder MapUserProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/userprofiles")
            .WithTags("UserProfiles")
            .WithDescription("User profile management endpoints");

        group.MapPost("/", CreateUserProfile)
            .WithName("CreateUserProfile")
            .WithSummary("Create a new user profile");

        group.MapPut("/{userId}", UpdateUserProfile)
            .WithName("UpdateUserProfile")
            .WithSummary("Update an existing user profile");

        group.MapGet("/{userId}", GetUserProfile)
            .WithName("GetUserProfile")
            .WithSummary("Get a user profile by user ID");

        group.MapGet("/", ListUserProfiles)
            .WithName("ListUserProfiles")
            .WithSummary("List all user profiles");

        group.MapGet("/paged", GetUserProfilesPaged)
            .WithName("GetUserProfilesPaged")
            .WithSummary("Get user profiles with pagination (10 per page)");

        group.MapGet("/paged/{pageNumber:int}", GetUserProfilesPage)
            .WithName("GetUserProfilesPage")
            .WithSummary("Get a specific page of user profiles");

        group.MapGet("/team-members", GetTeamMembers)
            .WithName("GetTeamMembers")
            .WithSummary("Get team members (non-stakeholders) for dropdowns");

        return group;
    }

    private static async Task<IResult> CreateUserProfile(
        [FromBody] CreateUserProfileRequest request,
        [FromServices] IUserProfileFactory factory)
    {
        // Use factory method to create profile - this handles ID generation and initial event
        var (result, userProfile) = await factory.CreateProfileAsync(
            request.DisplayName,
            request.Email,
            request.JobRole,
            createdByUser: null); // For user creation, createdByUser is null (system-created)

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

        var userId = userProfile!.Metadata!.Id!;
        return Results.Created($"/api/userprofiles/{userId.Value}", new
        {
            userId = userId.Value,
            displayName = request.DisplayName,
            email = request.Email,
            jobRole = request.JobRole
        });
    }

    private static async Task<IResult> UpdateUserProfile(
        string userId,
        [FromBody] UpdateUserProfileRequest request,
        [FromServices] IUserProfileFactory factory)
    {
        var userProfile = await factory.GetAsync(UserProfileId.From(userId));

        // Get the updating user's version token
        var updatingUserId = UserProfileId.From("api-user"); // TODO: Get from authentication context
        var updatingUserToken = await factory.GetUserVersionTokenAsync(updatingUserId);

        var result = await userProfile.UpdateProfile(
            request.DisplayName,
            request.Email,
            request.JobRole,
            updatingUserToken);

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

        return Results.Ok(new CommandResult(true, "User profile updated successfully"));
    }

    private static async Task<IResult> GetUserProfile(
        string userId,
        [FromServices] IUserProfileFactory factory)
    {
        var userProfile = await factory.GetAsync(UserProfileId.From(userId));

        var dto = new UserProfileDto(
            userId,
            userProfile.DisplayName ?? "",
            userProfile.Email ?? "",
            userProfile.JobRole ?? "",
            userProfile.CreatedAt,
            userProfile.LastUpdatedAt);

        return Results.Ok(dto);
    }

    private static IResult ListUserProfiles(
        [FromServices] IProjectionService projectionService)
    {
        var userProfiles = projectionService.GetUserProfiles();
        var profiles = userProfiles.GetAllProfiles();

        var dtos = profiles.Select(p => new UserProfileDto(
            p.UserId,
            p.Name,
            p.Email,
            p.JobRole,
            p.CreatedAt,
            p.UpdatedAt)).ToList();

        return Results.Ok(dtos);
    }

    private static IResult GetUserProfilesPaged(
        [FromServices] IProjectionService projectionService)
    {
        var userProfiles = projectionService.GetUserProfiles();

        return Results.Ok(new PaginationInfoDto(
            userProfiles.TotalUsers,
            userProfiles.TotalPages,
            10 // Users per page
        ));
    }

    private static IResult GetUserProfilesPage(
        int pageNumber,
        [FromServices] IProjectionService projectionService)
    {
        var userProfiles = projectionService.GetUserProfiles();

        // Handle empty projection - return empty page
        if (userProfiles.TotalPages == 0)
        {
            return Results.Ok(new UserProfilePageDto(
                1,
                0,
                0,
                new List<UserProfileDto>()
            ));
        }

        if (pageNumber < 1 || pageNumber > userProfiles.TotalPages)
        {
            return Results.NotFound(new { message = $"Page {pageNumber} not found. Total pages: {userProfiles.TotalPages}" });
        }

        var profiles = userProfiles.GetProfilesForPage(pageNumber);

        var dtos = profiles.Select(p => new UserProfileDto(
            p.UserId,
            p.Name,
            p.Email,
            p.JobRole,
            p.CreatedAt,
            p.UpdatedAt)).ToList();

        return Results.Ok(new UserProfilePageDto(
            pageNumber,
            userProfiles.TotalPages,
            userProfiles.TotalUsers,
            dtos
        ));
    }

    private static IResult GetTeamMembers(
        [FromServices] IProjectionService projectionService)
    {
        var userProfiles = projectionService.GetUserProfiles();
        var teamMembers = userProfiles.GetTeamMembers();

        var dtos = teamMembers.Select(m => new TeamMemberDto(
            m.UserId,
            m.DisplayName,
            m.Email)).ToList();

        return Results.Ok(dtos);
    }
}

// Request/Response DTOs
public record CreateUserProfileRequest(string UserId, string DisplayName, string Email, string JobRole);
public record UpdateUserProfileRequest(string DisplayName, string Email, string JobRole);
public record UserProfileDto(string UserId, string DisplayName, string Email, string JobRole, DateTime CreatedAt, DateTime? LastUpdatedAt);
public record PaginationInfoDto(int TotalUsers, int TotalPages, int UsersPerPage);
public record UserProfilePageDto(int PageNumber, int TotalPages, int TotalUsers, List<UserProfileDto> Users);
public record TeamMemberDto(string UserId, string DisplayName, string Email);
