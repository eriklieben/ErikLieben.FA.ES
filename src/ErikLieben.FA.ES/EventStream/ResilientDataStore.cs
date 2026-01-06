using System.Diagnostics;
using System.Net;
using System.Net.Http;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// A decorator for <see cref="IDataStore"/> that adds retry resilience for transient failures.
/// </summary>
/// <remarks>
/// <para>
/// This wrapper uses Polly to automatically retry operations that fail due to transient errors
/// such as network timeouts, rate limiting, or temporary service unavailability.
/// </para>
/// <para>
/// Retries are NOT performed for:
/// <list type="bullet">
/// <item>404 Not Found - Resource doesn't exist</item>
/// <item>409 Conflict - Optimistic concurrency violation</item>
/// <item>400 Bad Request - Invalid request format</item>
/// <item>401/403 - Authentication/authorization errors</item>
/// </list>
/// </para>
/// </remarks>
public class ResilientDataStore : IDataStore, IDataStoreRecovery
{
    private readonly IDataStore inner;
    private readonly ResiliencePipeline pipeline;
    private readonly ILogger<ResilientDataStore>? logger;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.ResilientDataStore");

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientDataStore"/> class.
    /// </summary>
    /// <param name="inner">The underlying data store to wrap.</param>
    /// <param name="options">The resilience options.</param>
    /// <param name="logger">Optional logger for retry events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="options"/> is null.</exception>
    public ResilientDataStore(
        IDataStore inner,
        DataStoreResilienceOptions options,
        ILogger<ResilientDataStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);

        this.inner = inner;
        this.logger = logger;
        this.pipeline = CreateResiliencePipeline(options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientDataStore"/> class with a custom resilience pipeline.
    /// </summary>
    /// <param name="inner">The underlying data store to wrap.</param>
    /// <param name="pipeline">The custom resilience pipeline to use.</param>
    /// <param name="logger">Optional logger for retry events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="pipeline"/> is null.</exception>
    public ResilientDataStore(
        IDataStore inner,
        ResiliencePipeline pipeline,
        ILogger<ResilientDataStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(pipeline);

        this.inner = inner;
        this.pipeline = pipeline;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task AppendAsync(IObjectDocument document, params IEvent[] events)
    {
        using var activity = ActivitySource.StartActivity("ResilientDataStore.AppendAsync");
        await pipeline.ExecuteAsync(
            async ct => await inner.AppendAsync(document, events),
            CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, params IEvent[] events)
    {
        using var activity = ActivitySource.StartActivity("ResilientDataStore.AppendAsync");
        await pipeline.ExecuteAsync(
            async ct => await inner.AppendAsync(document, preserveTimestamp, events),
            CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null)
    {
        using var activity = ActivitySource.StartActivity("ResilientDataStore.ReadAsync");
        return await pipeline.ExecuteAsync(
            async ct => await inner.ReadAsync(document, startVersion, untilVersion, chunk),
            CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task<int> RemoveEventsForFailedCommitAsync(
        IObjectDocument document,
        int fromVersion,
        int toVersion)
    {
        using var activity = ActivitySource.StartActivity("ResilientDataStore.RemoveEventsForFailedCommitAsync");
        return await pipeline.ExecuteAsync(
            async ct => await ((IDataStoreRecovery)inner).RemoveEventsForFailedCommitAsync(document, fromVersion, toVersion),
            CancellationToken.None);
    }

    private ResiliencePipeline CreateResiliencePipeline(DataStoreResilienceOptions options)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.InitialDelay,
                MaxDelay = options.MaxDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => IsTransientException(ex)),
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        args.Outcome.Exception,
                        "Retry attempt {AttemptNumber} after {Delay}ms for data store operation due to: {Error}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is transient; otherwise, false.</returns>
    private static bool IsTransientException(Exception exception)
    {
        // Check for HTTP status codes in various exception types
        var statusCode = GetStatusCodeFromException(exception);
        if (statusCode.HasValue)
        {
            return IsTransientStatusCode(statusCode.Value);
        }

        // Common transient exception types
        return exception is TimeoutException
            || exception is TaskCanceledException
            || exception is OperationCanceledException
            || exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Custom status code extractors for exception types not known at compile time.
    /// </summary>
    /// <remarks>
    /// Azure.RequestFailedException and CosmosException handlers should be registered
    /// by the respective provider packages to enable retry based on their status codes.
    /// </remarks>
    private static readonly List<Func<Exception, int?>> StatusCodeExtractors = [];

    /// <summary>
    /// Registers a custom status code extractor for exception types.
    /// </summary>
    /// <param name="extractor">A function that extracts a status code from an exception, or returns null if not applicable.</param>
    /// <remarks>
    /// Use this to register handlers for provider-specific exceptions like Azure.RequestFailedException
    /// or CosmosException. The extractor should return the HTTP status code or null.
    /// </remarks>
    public static void RegisterStatusCodeExtractor(Func<Exception, int?> extractor)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        StatusCodeExtractors.Add(extractor);
    }

    /// <summary>
    /// Clears all registered status code extractors.
    /// </summary>
    /// <remarks>Primarily for testing purposes.</remarks>
    public static void ClearStatusCodeExtractors()
    {
        StatusCodeExtractors.Clear();
    }

    /// <summary>
    /// Extracts HTTP status code from various exception types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses pattern matching for known .NET exception types (HttpRequestException)
    /// and registered extractors for provider-specific exceptions.
    /// </para>
    /// <para>
    /// AOT-compatible: Does not use reflection. Provider packages should register
    /// their own extractors via <see cref="RegisterStatusCodeExtractor"/>.
    /// </para>
    /// </remarks>
    private static int? GetStatusCodeFromException(Exception exception)
    {
        // HttpRequestException has StatusCode property in .NET 5+
        if (exception is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
        {
            return (int)httpEx.StatusCode.Value;
        }

        // Try registered extractors for provider-specific exceptions
        // (Azure.RequestFailedException, CosmosException, etc.)
        foreach (var extractor in StatusCodeExtractors)
        {
            var statusCode = extractor(exception);
            if (statusCode.HasValue)
            {
                return statusCode;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if an HTTP status code is transient.
    /// </summary>
    private static bool IsTransientStatusCode(int statusCode) => statusCode switch
    {
        408 => true,  // Request Timeout
        429 => true,  // Too Many Requests (rate limiting)
        500 => true,  // Internal Server Error
        502 => true,  // Bad Gateway
        503 => true,  // Service Unavailable
        504 => true,  // Gateway Timeout
        _ => false
    };

    /// <summary>
    /// Creates a default resilience pipeline with standard options.
    /// </summary>
    /// <param name="options">The resilience options.</param>
    /// <returns>A configured resilience pipeline.</returns>
    public static ResiliencePipeline CreateDefaultPipeline(DataStoreResilienceOptions? options = null)
    {
        options ??= new DataStoreResilienceOptions();

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.InitialDelay,
                MaxDelay = options.MaxDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => IsTransientException(ex))
            })
            .Build();
    }
}
