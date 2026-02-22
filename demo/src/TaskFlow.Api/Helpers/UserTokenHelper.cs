using ErikLieben.FA.ES;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.Helpers;

/// <summary>
/// Helper class for creating VersionTokens from UserProfile aggregates
/// </summary>
public static class UserTokenHelper
{
    /// <summary>
    /// Creates a VersionToken from a UserProfileId by loading the current state
    /// </summary>
    public static async Task<VersionToken?> GetUserVersionTokenAsync(
        this IUserProfileFactory factory,
        UserProfileId userId)
    {
        try
        {
            var userProfile = await factory.GetAsync(userId);

            if (userProfile?.Metadata == null)
                return null;

            return userProfile.Metadata.ToVersionToken("userprofile");
        }
        catch
        {
            // If user profile doesn't exist or can't be loaded, return null
            return null;
        }
    }

    /// <summary>
    /// Gets the current user ID and VersionToken from the current request context
    /// </summary>
    public static async Task<(UserProfileId userId, VersionToken? token)> GetCurrentUserAsync(
        this IUserProfileFactory factory,
        ICurrentUserService currentUserService)
    {
        var userId = currentUserService.GetCurrentUserId() ?? throw new InvalidOperationException("No current user found");
        var userProfileId = UserProfileId.From(userId);
        var token = await factory.GetUserVersionTokenAsync(userProfileId);
        return (userProfileId, token);
    }
}
