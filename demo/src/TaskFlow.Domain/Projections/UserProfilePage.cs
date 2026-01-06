using System.Collections.Generic;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.UserProfile;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Destination projection representing a single page of user profiles.
/// Each page contains up to 10 users, stored as a separate JSON file.
/// The {pageNumber} placeholder is replaced with the actual page number.
/// </summary>
[BlobJsonProjection("projections/userprofiles/page-{pageNumber}.json", Connection = "BlobStorage")]
public partial class UserProfilePage : Projection
{
    /// <summary>
    /// The page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// User profiles on this page, keyed by user ID.
    /// </summary>
    public Dictionary<string, UserProfileInfo> Users { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(UserProfileCreated @event, string userId)
    {
        Users[userId] = new UserProfileInfo
        {
            UserId = userId,
            Name = @event.DisplayName,
            Email = @event.Email,
            JobRole = @event.JobRole,
            CreatedAt = @event.CreatedAt,
            UpdatedAt = @event.CreatedAt
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(UserProfileUpdated @event, string userId)
    {
        if (Users.TryGetValue(userId, out var existing))
        {
            Users[userId] = existing with
            {
                Name = @event.DisplayName,
                Email = @event.Email,
                JobRole = @event.JobRole,
                UpdatedAt = @event.UpdatedAt
            };
        }
    }
}
