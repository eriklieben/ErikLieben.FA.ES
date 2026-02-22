using System.Collections.Generic;
using System.Linq;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.UserProfile;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Destination projection containing only team members (non-stakeholders).
/// This is a small, fast-loading projection for dropdowns and selectors.
/// </summary>
[ProjectionWithExternalCheckpoint]
[BlobJsonProjection("projections/userprofiles/team-members.json", Connection = "BlobStorage")]
public partial class TeamMembers : Projection
{
    /// <summary>
    /// Team members keyed by user ID.
    /// </summary>
    public Dictionary<string, TeamMemberInfo> Members { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(UserProfileCreated @event, string userId)
    {
        // Only add non-stakeholders
        if (!@event.Email.EndsWith("@stakeholders.com"))
        {
            Members[userId] = new TeamMemberInfo
            {
                UserId = userId,
                DisplayName = @event.DisplayName,
                Email = @event.Email
            };
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(UserProfileUpdated @event, string userId)
    {
        // If already a member, update it
        if (Members.ContainsKey(userId))
        {
            // Check if they're still not a stakeholder
            if (!@event.Email.EndsWith("@stakeholders.com"))
            {
                Members[userId] = new TeamMemberInfo
                {
                    UserId = userId,
                    DisplayName = @event.DisplayName,
                    Email = @event.Email
                };
            }
            else
            {
                // They became a stakeholder, remove from team members
                Members.Remove(userId);
            }
        }
        else if (!@event.Email.EndsWith("@stakeholders.com"))
        {
            // They were a stakeholder but now aren't, add to team members
            Members[userId] = new TeamMemberInfo
            {
                UserId = userId,
                DisplayName = @event.DisplayName,
                Email = @event.Email
            };
        }
    }

    /// <summary>
    /// Gets all team members ordered by display name.
    /// </summary>
    public List<TeamMemberInfo> GetAll() => Members.Values.OrderBy(m => m.DisplayName).ToList();
}

/// <summary>
/// Minimal info for team member dropdowns.
/// </summary>
public record TeamMemberInfo
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
}
