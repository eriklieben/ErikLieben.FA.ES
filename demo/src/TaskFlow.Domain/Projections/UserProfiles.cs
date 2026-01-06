using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.UserProfile;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Routed projection that manages user profiles across multiple pages.
/// Each page contains up to 10 users, stored as a separate JSON file.
/// The main projection tracks page assignments and total page count.
/// </summary>
[BlobJsonProjection("projections/userprofiles.json", Connection = "BlobStorage")]
[ProjectionWithExternalCheckpoint]
public partial class UserProfiles : RoutedProjection
{
    private const int UsersPerPage = 10;

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Total number of users across all pages.
    /// </summary>
    public int TotalUsers { get; set; }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(UserProfileCreated @event, string userId)
    {
        // Calculate which page this user should go to
        int pageNumber = (TotalUsers / UsersPerPage) + 1;

        // Ensure the page destination exists
        var pageKey = $"page-{pageNumber}";
        AddDestination<UserProfilePage>(pageKey, new Dictionary<string, string> { ["pageNumber"] = pageNumber.ToString() });

        // Ensure the team members destination exists
        AddDestination<TeamMembers>("team-members");

        // Update totals
        TotalUsers++;
        TotalPages = Math.Max(TotalPages, pageNumber);

        // Route the event to the appropriate page
        RouteToDestination(pageKey);

        // Also route to team members (the destination will filter non-stakeholders)
        RouteToDestination("team-members");
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(UserProfileUpdated @event, string userId)
    {
        // Find which page this user is on by searching destinations
        var pageNumber = FindPageForUser(userId);
        if (pageNumber > 0)
        {
            var pageKey = $"page-{pageNumber}";
            RouteToDestination(pageKey);
        }

        // Also route to team members for potential membership changes
        RouteToDestination("team-members");
    }

    /// <summary>
    /// Finds which page a user is on by searching through destinations.
    /// </summary>
    private int FindPageForUser(string userId)
    {
        for (int i = 1; i <= TotalPages; i++)
        {
            if (TryGetDestination<UserProfilePage>($"page-{i}", out var page) && page != null && page.Users.ContainsKey(userId))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Gets team members (non-stakeholders) for dropdowns.
    /// This loads only the team-members destination file.
    /// </summary>
    public List<TeamMemberInfo> GetTeamMembers()
    {
        if (TryGetDestination<TeamMembers>("team-members", out var teamMembers) && teamMembers != null)
        {
            return teamMembers.GetAll();
        }
        return new List<TeamMemberInfo>();
    }

    /// <summary>
    /// Gets all user profiles from all pages, ordered by name.
    /// Note: This loads all destination pages.
    /// </summary>
    public List<UserProfileInfo> GetAllProfiles()
    {
        var allProfiles = new List<UserProfileInfo>();

        foreach (var pageKey in GetDestinationKeys())
        {
            if (TryGetDestination<UserProfilePage>(pageKey, out var page) && page != null)
            {
                allProfiles.AddRange(page.Users.Values);
            }
        }

        return allProfiles.OrderBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Gets user profiles for a specific page.
    /// </summary>
    public List<UserProfileInfo> GetProfilesForPage(int pageNumber)
    {
        var pageKey = $"page-{pageNumber}";
        if (TryGetDestination<UserProfilePage>(pageKey, out var page) && page != null)
        {
            return page.Users.Values.OrderBy(p => p.Name).ToList();
        }
        return new List<UserProfileInfo>();
    }

    /// <summary>
    /// Gets a specific user profile by ID.
    /// Searches through pages to find the user.
    /// </summary>
    public UserProfileInfo? GetProfile(string userId)
    {
        var pageNumber = FindPageForUser(userId);
        if (pageNumber < 0)
        {
            return null;
        }

        var pageKey = $"page-{pageNumber}";
        if (TryGetDestination<UserProfilePage>(pageKey, out var page) && page != null)
        {
            return page.Users.TryGetValue(userId, out var profile) ? profile : null;
        }

        return null;
    }
}

/// <summary>
/// Information about a user profile (stored in page destinations).
/// </summary>
public record UserProfileInfo
{
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string JobRole { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
