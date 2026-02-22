using TaskFlow.Api.Services;
using TaskFlow.Api.Endpoints;

namespace TaskFlow.Api.Middleware;

/// <summary>
/// Middleware that extracts the current user from the request header (for demo mode).
/// In production, this would be replaced with proper authentication middleware.
/// </summary>
public class CurrentUserMiddleware
{
    private const string CURRENT_USER_HEADER = "X-Current-User";
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CurrentUserMiddleware> _logger;
    private bool _warningLogged;

    public CurrentUserMiddleware(RequestDelegate next, IWebHostEnvironment environment, ILogger<CurrentUserMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUserService)
    {
        if (!_environment.IsDevelopment() && !_warningLogged)
        {
            _logger.LogWarning(
                "CurrentUserMiddleware is active in a non-development environment. " +
                "This middleware trusts the X-Current-User header without authentication and should only be used during development.");
            _warningLogged = true;
        }
        // Try to get the user ID from the header
        string? userId = null;

        if (context.Request.Headers.TryGetValue(CURRENT_USER_HEADER, out var headerValue))
        {
            userId = headerValue.FirstOrDefault();
        }

        // If no user specified, default to admin user for demo purposes
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = UserProfileEndpoints.ADMIN_USER_ID;
        }

        // Set the current user for this request
        currentUserService.SetCurrentUserId(userId);

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the middleware
/// </summary>
public static class CurrentUserMiddlewareExtensions
{
    public static IApplicationBuilder UseCurrentUser(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CurrentUserMiddleware>();
    }
}
