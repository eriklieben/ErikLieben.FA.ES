using System.Diagnostics;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Observability;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Executes post-commit actions with configurable retry behavior.
/// </summary>
/// <remarks>
/// <para>
/// This executor wraps post-commit actions with retry logic to handle transient failures.
/// It tracks attempt counts and timing for diagnostic purposes.
/// </para>
/// <para>
/// Important: Even if retries are exhausted, the events have already been committed.
/// Failed actions are reported via <see cref="FailedPostCommitAction"/> results.
/// </para>
/// </remarks>
public class ResilientPostCommitActionExecutor
{
    private readonly ResiliencePipeline pipeline;
    private readonly ILogger<ResilientPostCommitActionExecutor>? logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientPostCommitActionExecutor"/> class.
    /// </summary>
    /// <param name="options">The retry configuration options.</param>
    /// <param name="logger">Optional logger for retry events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public ResilientPostCommitActionExecutor(
        PostCommitRetryOptions options,
        ILogger<ResilientPostCommitActionExecutor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.logger = logger;
        pipeline = CreateResiliencePipeline(options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientPostCommitActionExecutor"/> class
    /// with a custom resilience pipeline.
    /// </summary>
    /// <param name="pipeline">The custom resilience pipeline to use.</param>
    /// <param name="logger">Optional logger for retry events.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pipeline"/> is null.</exception>
    public ResilientPostCommitActionExecutor(
        ResiliencePipeline pipeline,
        ILogger<ResilientPostCommitActionExecutor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        this.pipeline = pipeline;
        this.logger = logger;
    }

    /// <summary>
    /// Executes a post-commit action with retry behavior.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="events">The events that were committed.</param>
    /// <param name="document">The object document associated with the stream.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure with diagnostic information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public async Task<PostCommitActionResult> ExecuteAsync(
        IAsyncPostCommitAction action,
        IEnumerable<JsonEvent> events,
        IObjectDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(document);

        var actionName = action.GetType().Name;
        using var activity = FaesInstrumentation.Core.StartActivity($"PostCommit.{actionName}");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.ActionType, actionName);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
        }

        var actionType = action.GetType();
        var startTime = Stopwatch.GetTimestamp();
        var attempts = 0;
        var eventsList = events.ToList();

        activity?.SetTag(FaesSemanticConventions.EventCount, eventsList.Count);

        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            logger.LogDebug(
                "Starting post-commit action {ActionType} for {EventCount} events",
                actionName,
                eventsList.Count);
        }

        try
        {
            await pipeline.ExecuteAsync(async token =>
            {
                attempts++;
                activity?.SetTag(FaesSemanticConventions.RetryAttempt, attempts);

                if (logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    logger.LogDebug(
                        "Executing post-commit action {ActionType}, attempt {Attempt}",
                        actionName,
                        attempts);
                }

                await action.PostCommitAsync(eventsList, document);
            }, cancellationToken);

            var duration = Stopwatch.GetElapsedTime(startTime);
            activity?.SetTag(FaesSemanticConventions.Success, true);
            activity?.SetTag(FaesSemanticConventions.DurationMs, duration.TotalMilliseconds);

            if (logger?.IsEnabled(LogLevel.Debug) == true)
            {
                logger.LogDebug(
                    "Post-commit action {ActionType} succeeded after {Attempts} attempt(s) in {Duration}ms",
                    actionName,
                    attempts,
                    duration.TotalMilliseconds);
            }

            return PostCommitActionResult.Succeeded(actionName, actionType, duration);
        }
        catch (Exception ex)
        {
            var duration = Stopwatch.GetElapsedTime(startTime);
            FaesInstrumentation.RecordException(activity, ex);
            activity?.SetTag(FaesSemanticConventions.Success, false);
            activity?.SetTag(FaesSemanticConventions.DurationMs, duration.TotalMilliseconds);

            if (logger?.IsEnabled(LogLevel.Warning) == true)
            {
                logger.LogWarning(
                    ex,
                    "Post-commit action {ActionType} failed after {Attempts} attempt(s) in {Duration}ms: {Error}",
                    actionName,
                    attempts,
                    duration.TotalMilliseconds,
                    ex.Message);
            }

            return PostCommitActionResult.Failed(actionName, actionType, ex, attempts, duration);
        }
    }

    /// <summary>
    /// Executes multiple post-commit actions in sequence with retry behavior.
    /// </summary>
    /// <param name="actions">The actions to execute.</param>
    /// <param name="events">The events that were committed.</param>
    /// <param name="document">The object document associated with the stream.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of results for each action.</returns>
    public async Task<IReadOnlyList<PostCommitActionResult>> ExecuteAllAsync(
        IEnumerable<IAsyncPostCommitAction> actions,
        IEnumerable<JsonEvent> events,
        IObjectDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(document);

        var results = new List<PostCommitActionResult>();
        var eventsList = events.ToList();

        foreach (var action in actions)
        {
            var result = await ExecuteAsync(action, eventsList, document, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private ResiliencePipeline CreateResiliencePipeline(PostCommitRetryOptions options)
    {
        if (options.MaxRetries == 0)
        {
            return ResiliencePipeline.Empty;
        }

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetries,
                Delay = options.InitialDelay,
                MaxDelay = options.MaxDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                OnRetry = args =>
                {
                    if (logger?.IsEnabled(LogLevel.Warning) == true)
                    {
                        logger.LogWarning(
                            args.Outcome.Exception,
                            "Post-commit action retry attempt {AttemptNumber} after {Delay}ms due to: {Error}",
                            args.AttemptNumber,
                            args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.Message);
                    }
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a default executor with standard options.
    /// </summary>
    /// <param name="logger">Optional logger for retry events.</param>
    /// <returns>A new executor instance with default options.</returns>
    public static ResilientPostCommitActionExecutor CreateDefault(
        ILogger<ResilientPostCommitActionExecutor>? logger = null)
    {
        return new ResilientPostCommitActionExecutor(PostCommitRetryOptions.Default, logger);
    }
}
