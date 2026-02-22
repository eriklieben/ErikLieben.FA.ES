using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Actions;
using TaskFlow.Api.Helpers;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.ValueObjects;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Validation;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Demo endpoints for showcasing idempotency features.
/// </summary>
/// <remarks>
/// These endpoints demonstrate:
/// <list type="bullet">
/// <item>Decision Checkpoint Pattern - validating user decisions are based on current state</item>
/// <item>Post-commit action failure handling</item>
/// <item>Stale decision detection</item>
/// </list>
/// </remarks>
public static class IdempotencyDemoEndpoints
{
    private const string DecisionCheckpointHeader = "X-Decision-Checkpoint";

    public static RouteGroupBuilder MapIdempotencyDemoEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/demo/idempotency")
            .WithTags("Idempotency Demo")
            .WithDescription("Endpoints demonstrating idempotency patterns");

        // Checkpoint validation demo
        group.MapGet("/workitems/{id}/with-checkpoint", GetWorkItemWithCheckpoint)
            .WithName("GetWorkItemWithCheckpoint")
            .WithSummary("Get work item with checkpoint fingerprint for optimistic concurrency");

        group.MapPost("/workitems/{id}/complete-validated", CompleteWorkWithCheckpointValidation)
            .WithName("CompleteWorkWithCheckpointValidation")
            .WithSummary("Complete work with decision checkpoint validation");

        // Stale decision demo
        group.MapPost("/workitems/{id}/simulate-stale-decision", SimulateStaleDecision)
            .WithName("SimulateStaleDecision")
            .WithSummary("Simulate a stale decision scenario for testing");

        // Post-commit failure demo
        group.MapPost("/workitems/{id}/complete-with-webhook", CompleteWorkWithWebhook)
            .WithName("CompleteWorkWithWebhook")
            .WithSummary("Complete work triggering demo webhook (may fail based on config)");

        // Webhook configuration
        group.MapPost("/webhook/configure", ConfigureWebhook)
            .WithName("ConfigureWebhook")
            .WithSummary("Configure demo webhook behavior");

        group.MapGet("/webhook/status", GetWebhookStatus)
            .WithName("GetWebhookStatus")
            .WithSummary("Get current demo webhook configuration");

        return group;
    }

    /// <summary>
    /// Gets a work item with its checkpoint fingerprint included.
    /// </summary>
    /// <remarks>
    /// The checkpoint fingerprint should be stored by the client and included
    /// in subsequent state-changing requests via the X-Decision-Checkpoint header.
    /// </remarks>
    private static async Task<IResult> GetWorkItemWithCheckpoint(
        string id,
        [FromServices] IWorkItemFactory factory)
    {
        var workItem = await factory.GetAsync(WorkItemId.From(id));

        // Build checkpoint from the aggregate's event stream
        var streamId = workItem.EventStream.StreamIdentifier;
        var version = workItem.EventStream.CurrentVersion;

        // Create a simple checkpoint fingerprint (in real scenarios, this would include
        // all projections the user viewed)
        var checkpointFingerprint = $"{streamId}:v{version}";

        return Results.Ok(new
        {
            id = id,
            title = workItem.Title,
            description = workItem.Description,
            status = workItem.Status.ToString(),
            priority = workItem.Priority,
            assignedTo = workItem.AssignedTo,

            // Checkpoint information
            checkpoint = new
            {
                fingerprint = checkpointFingerprint,
                streamId = streamId,
                version = version,
                message = "Include the fingerprint in X-Decision-Checkpoint header for state-changing requests"
            }
        });
    }

    /// <summary>
    /// Completes work with checkpoint validation - demonstrates stale decision detection.
    /// </summary>
    private static async Task<IResult> CompleteWorkWithCheckpointValidation(
        string id,
        [FromHeader(Name = DecisionCheckpointHeader)] string? checkpointFingerprint,
        [FromBody] CompleteWorkValidatedRequest request,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromServices] IUserProfileFactory userProfileFactory)
    {
        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));

        // Validate checkpoint if provided
        if (!string.IsNullOrEmpty(checkpointFingerprint))
        {
            var context = DecisionContext.FromFingerprint(checkpointFingerprint);
            var validation = workItem.ValidateCheckpoint(context);

            if (!validation.IsValid)
            {
                return Results.Conflict(new
                {
                    error = "STALE_DECISION",
                    message = validation.Message,
                    details = new
                    {
                        streamId = validation.StreamId,
                        expectedVersion = validation.ExpectedVersion,
                        actualVersion = validation.ActualVersion
                    },
                    suggestion = "Please refresh the work item and try again"
                });
            }
        }

        // Proceed with the operation
        var result = await workItem.CompleteWork(request.Outcome, userId, userToken);

        if (!result.IsSuccess)
        {
            return Results.BadRequest(new
            {
                errors = result.Errors.ToArray().Select(e => new
                {
                    property = e.PropertyName,
                    message = e.Message
                })
            });
        }

        // Return success with the NEW checkpoint
        return Results.Ok(new
        {
            success = true,
            message = "Work completed successfully",
            newCheckpoint = new
            {
                fingerprint = $"{workItem.EventStream.StreamIdentifier}:v{workItem.EventStream.CurrentVersion}",
                streamId = workItem.EventStream.StreamIdentifier,
                version = workItem.EventStream.CurrentVersion
            }
        });
    }

    /// <summary>
    /// Simulates a stale decision scenario for testing purposes.
    /// </summary>
    private static async Task<IResult> SimulateStaleDecision(
        string id,
        [FromBody] SimulateStaleDecisionRequest request,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromServices] IUserProfileFactory userProfileFactory)
    {
        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));

        // Store the current version
        var initialVersion = workItem.EventStream.CurrentVersion;

        // Make a change to increment the version (simulate another user's change)
        await workItem.ProvideFeedback("Background change to simulate concurrent modification", userId, userToken);

        var newVersion = workItem.EventStream.CurrentVersion;

        // Now simulate validation with the OLD version
        var staleContext = DecisionContext.FromFingerprint(
            $"{workItem.EventStream.StreamIdentifier}:v{request.StaleVersion ?? initialVersion}");
        var validation = workItem.ValidateCheckpoint(staleContext);

        return Results.Ok(new
        {
            scenario = "Stale decision simulation",
            steps = new[]
            {
                $"1. Initial version was {initialVersion}",
                $"2. Background change made - version now {newVersion}",
                $"3. Validated against stale version {request.StaleVersion ?? initialVersion}"
            },
            validationResult = new
            {
                isValid = validation.IsValid,
                message = validation.Message,
                expectedVersion = validation.ExpectedVersion,
                actualVersion = validation.ActualVersion
            },
            recommendation = validation.IsValid
                ? "Validation passed (unexpected in this demo)"
                : "Client should show 'refresh required' dialog"
        });
    }

    /// <summary>
    /// Completes work with a demo webhook that may fail.
    /// </summary>
    private static async Task<IResult> CompleteWorkWithWebhook(
        string id,
        [FromBody] CompleteWorkValidatedRequest request,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] DemoWebhookOptions webhookOptions)
    {
        var userId = UserProfileId.From("api-user");
        var userToken = await userProfileFactory.GetUserVersionTokenAsync(userId);

        var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));

        try
        {
            var result = await workItem.CompleteWork(request.Outcome, userId, userToken);

            if (!result.IsSuccess)
            {
                return Results.BadRequest(new
                {
                    errors = result.Errors.ToArray().Select(e => new { e.PropertyName, e.Message })
                });
            }

            return Results.Ok(new
            {
                success = true,
                message = "Work completed and webhook triggered",
                webhookConfig = new
                {
                    simulateFailure = webhookOptions.SimulateFailure,
                    failFirstNAttempts = webhookOptions.FailFirstNAttempts
                }
            });
        }
        catch (ErikLieben.FA.ES.Exceptions.PostCommitActionFailedException ex)
        {
            // Events are committed but post-commit action failed
            return Results.Ok(new
            {
                success = true, // Events ARE committed
                warning = "Post-commit action failed",
                details = new
                {
                    errorCode = ex.ErrorCode,
                    streamId = ex.StreamId,
                    committedEventCount = ex.CommittedEvents.Count,
                    committedVersionRange = ex.CommittedVersionRange,
                    failedActions = ex.FailedActionNames,
                    succeededActions = ex.SucceededActionNames,
                    firstError = ex.FirstError?.Message
                },
                recommendation = "Events were persisted. Webhook failure should be handled by retry/dead-letter queue."
            });
        }
    }

    /// <summary>
    /// Configures the demo webhook behavior.
    /// </summary>
    private static IResult ConfigureWebhook(
        [FromBody] ConfigureWebhookRequest request,
        [FromServices] DemoWebhookOptions options)
    {
        options.SimulateFailure = request.SimulateFailure ?? options.SimulateFailure;
        options.FailFirstNAttempts = request.FailFirstNAttempts ?? options.FailFirstNAttempts;
        options.SimulatedLatency = request.SimulatedLatencyMs.HasValue
            ? TimeSpan.FromMilliseconds(request.SimulatedLatencyMs.Value)
            : options.SimulatedLatency;

        return Results.Ok(new
        {
            message = "Webhook configuration updated",
            config = new
            {
                simulateFailure = options.SimulateFailure,
                failFirstNAttempts = options.FailFirstNAttempts,
                simulatedLatencyMs = options.SimulatedLatency.TotalMilliseconds
            }
        });
    }

    /// <summary>
    /// Gets the current demo webhook configuration.
    /// </summary>
    private static IResult GetWebhookStatus([FromServices] DemoWebhookOptions options)
    {
        return Results.Ok(new
        {
            config = new
            {
                simulateFailure = options.SimulateFailure,
                failFirstNAttempts = options.FailFirstNAttempts,
                simulatedLatencyMs = options.SimulatedLatency.TotalMilliseconds
            },
            description = new
            {
                simulateFailure = "When true, webhook always fails",
                failFirstNAttempts = "Number of initial failures before success (for retry testing)",
                simulatedLatencyMs = "Artificial delay before webhook completes"
            }
        });
    }
}

/// <summary>
/// Request to complete work with validation.
/// </summary>
public record CompleteWorkValidatedRequest(string Outcome);

/// <summary>
/// Request to simulate a stale decision.
/// </summary>
public record SimulateStaleDecisionRequest(int? StaleVersion);

/// <summary>
/// Request to configure the demo webhook.
/// </summary>
public record ConfigureWebhookRequest(
    bool? SimulateFailure,
    int? FailFirstNAttempts,
    int? SimulatedLatencyMs);
