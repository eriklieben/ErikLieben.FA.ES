using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Api.Middleware;

namespace TaskFlow.Api.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private readonly ILogger<GlobalExceptionHandler> _logger = NullLogger<GlobalExceptionHandler>.Instance;

    private static Microsoft.AspNetCore.Hosting.IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var env = new TestWebHostEnvironment { EnvironmentName = environmentName };
        return env;
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ProblemDetails?> ReadProblemDetails(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    [Fact]
    public async Task TryHandleAsync_InDevelopment_IncludesExceptionMessage()
    {
        var env = CreateEnvironment(Environments.Development);
        var handler = new GlobalExceptionHandler(_logger, env);
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Sensitive error details here");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);

        var problem = await ReadProblemDetails(context);
        Assert.NotNull(problem);
        Assert.Equal("Sensitive error details here", problem.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_InDevelopment_IncludesStackTraceAndExceptionType()
    {
        var env = CreateEnvironment(Environments.Development);
        var handler = new GlobalExceptionHandler(_logger, env);
        var context = CreateHttpContext();

        Exception exception;
        try
        {
            throw new ArgumentException("bad argument");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);

        Assert.True(json.TryGetProperty("stackTrace", out var stackTrace));
        Assert.False(string.IsNullOrEmpty(stackTrace.GetString()));

        Assert.True(json.TryGetProperty("exceptionType", out var exType));
        Assert.Equal("ArgumentException", exType.GetString());
    }

    [Fact]
    public async Task TryHandleAsync_InProduction_HidesExceptionMessage()
    {
        var env = CreateEnvironment(Environments.Production);
        var handler = new GlobalExceptionHandler(_logger, env);
        var context = CreateHttpContext();
        var exception = new InvalidOperationException("Sensitive error details here");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        var problem = await ReadProblemDetails(context);
        Assert.NotNull(problem);
        Assert.Equal("An internal error occurred. Check logs for details.", problem.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_InProduction_DoesNotIncludeStackTrace()
    {
        var env = CreateEnvironment(Environments.Production);
        var handler = new GlobalExceptionHandler(_logger, env);
        var context = CreateHttpContext();

        Exception exception;
        try
        {
            throw new ArgumentException("bad argument");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);

        Assert.False(json.TryGetProperty("stackTrace", out _));
        Assert.False(json.TryGetProperty("exceptionType", out _));
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task TryHandleAsync_NonDevelopmentEnvironments_ReturnGenericMessage(string environmentName)
    {
        var env = CreateEnvironment(environmentName);
        var handler = new GlobalExceptionHandler(_logger, env);
        var context = CreateHttpContext();
        var exception = new Exception("Secret DB password is xyz");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        var problem = await ReadProblemDetails(context);
        Assert.NotNull(problem);
        Assert.DoesNotContain("xyz", problem.Detail);
        Assert.DoesNotContain("Secret", problem.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_SetsJsonContentType()
    {
        var env = CreateEnvironment(Environments.Production);
        var handler = new GlobalExceptionHandler(_logger, env);
        var context = CreateHttpContext();
        var exception = new Exception("test");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        // WriteAsJsonAsync serializes as JSON; content type includes application/json
        Assert.Contains("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task TryHandleAsync_SetsRequestPathAsInstance()
    {
        var env = CreateEnvironment(Environments.Development);
        var handler = new GlobalExceptionHandler(_logger, env);
        var context = CreateHttpContext();
        context.Request.Path = "/api/admin/events/project/123";
        var exception = new Exception("test");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        var problem = await ReadProblemDetails(context);
        Assert.NotNull(problem);
        Assert.Equal("/api/admin/events/project/123", problem.Instance);
    }

    /// <summary>
    /// Minimal IWebHostEnvironment implementation for testing.
    /// </summary>
    private class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "TestApp";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
