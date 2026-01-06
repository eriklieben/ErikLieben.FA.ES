namespace TaskFlow.Api.Services;

/// <summary>
/// Service for accessing the current user context for the request.
/// In demo mode, this is set via a header. In production, this would come from authentication.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user ID from the request context
    /// </summary>
    string? GetCurrentUserId();

    /// <summary>
    /// Sets the current user ID (used by middleware)
    /// </summary>
    void SetCurrentUserId(string userId);
}

/// <summary>
/// Implementation that stores the current user ID in AsyncLocal for the request
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private static readonly AsyncLocal<string?> _currentUserId = new();

    public string? GetCurrentUserId()
    {
        return _currentUserId.Value;
    }

    public void SetCurrentUserId(string userId)
    {
        _currentUserId.Value = userId;
    }
}
