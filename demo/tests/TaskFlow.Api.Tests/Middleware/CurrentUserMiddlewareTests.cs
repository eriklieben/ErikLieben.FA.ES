using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Endpoints;
using TaskFlow.Api.Middleware;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Tests.Middleware;

public class CurrentUserMiddlewareTests
{
    private readonly ILogger<CurrentUserMiddleware> _logger = NullLogger<CurrentUserMiddleware>.Instance;

    private static Microsoft.AspNetCore.Hosting.IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        return new TestWebHostEnvironment { EnvironmentName = environmentName };
    }

    [Fact]
    public async Task InvokeAsync_WithUserHeader_SetsUserFromHeader()
    {
        var env = CreateEnvironment(Environments.Development);
        var userService = new CurrentUserService();
        string? capturedUserId = null;

        var middleware = new CurrentUserMiddleware(
            next: async _ => { capturedUserId = userService.GetCurrentUserId(); },
            environment: env,
            logger: _logger);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Current-User"] = "user-42";

        await middleware.InvokeAsync(context, userService);

        Assert.Equal("user-42", capturedUserId);
    }

    [Fact]
    public async Task InvokeAsync_WithoutUserHeader_DefaultsToAdminUser()
    {
        var env = CreateEnvironment(Environments.Development);
        var userService = new CurrentUserService();
        string? capturedUserId = null;

        var middleware = new CurrentUserMiddleware(
            next: async _ => { capturedUserId = userService.GetCurrentUserId(); },
            environment: env,
            logger: _logger);

        var context = new DefaultHttpContext();
        // No X-Current-User header set

        await middleware.InvokeAsync(context, userService);

        Assert.Equal(UserProfileEndpoints.ADMIN_USER_ID, capturedUserId);
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyHeader_DefaultsToAdminUser()
    {
        var env = CreateEnvironment(Environments.Development);
        var userService = new CurrentUserService();
        string? capturedUserId = null;

        var middleware = new CurrentUserMiddleware(
            next: async _ => { capturedUserId = userService.GetCurrentUserId(); },
            environment: env,
            logger: _logger);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Current-User"] = "";

        await middleware.InvokeAsync(context, userService);

        Assert.Equal(UserProfileEndpoints.ADMIN_USER_ID, capturedUserId);
    }

    [Fact]
    public async Task InvokeAsync_WithWhitespaceHeader_DefaultsToAdminUser()
    {
        var env = CreateEnvironment(Environments.Development);
        var userService = new CurrentUserService();
        string? capturedUserId = null;

        var middleware = new CurrentUserMiddleware(
            next: async _ => { capturedUserId = userService.GetCurrentUserId(); },
            environment: env,
            logger: _logger);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Current-User"] = "   ";

        await middleware.InvokeAsync(context, userService);

        Assert.Equal(UserProfileEndpoints.ADMIN_USER_ID, capturedUserId);
    }

    [Fact]
    public async Task InvokeAsync_InNonDevelopment_LogsWarningOnce()
    {
        var env = CreateEnvironment(Environments.Production);
        var logEntries = new List<string>();
        var logger = new CapturingLogger<CurrentUserMiddleware>(logEntries);
        var userService = new CurrentUserService();

        var middleware = new CurrentUserMiddleware(
            next: _ => Task.CompletedTask,
            environment: env,
            logger: logger);

        var context1 = new DefaultHttpContext();
        var context2 = new DefaultHttpContext();

        await middleware.InvokeAsync(context1, userService);
        await middleware.InvokeAsync(context2, userService);

        // Warning should be logged only once
        var warningCount = logEntries.Count(e => e.Contains("non-development"));
        Assert.Equal(1, warningCount);
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_DoesNotLogWarning()
    {
        var env = CreateEnvironment(Environments.Development);
        var logEntries = new List<string>();
        var logger = new CapturingLogger<CurrentUserMiddleware>(logEntries);
        var userService = new CurrentUserService();

        var middleware = new CurrentUserMiddleware(
            next: _ => Task.CompletedTask,
            environment: env,
            logger: logger);

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context, userService);

        var warningCount = logEntries.Count(e => e.Contains("non-development"));
        Assert.Equal(0, warningCount);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var env = CreateEnvironment(Environments.Development);
        var userService = new CurrentUserService();
        bool nextCalled = false;

        var middleware = new CurrentUserMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            environment: env,
            logger: _logger);

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context, userService);

        Assert.True(nextCalled);
    }

    private class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "TestApp";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    /// <summary>
    /// Simple logger that captures log messages for assertion.
    /// </summary>
    private class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<string> _entries;

        public CapturingLogger(List<string> entries)
        {
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _entries.Add(formatter(state, exception));
        }
    }
}
