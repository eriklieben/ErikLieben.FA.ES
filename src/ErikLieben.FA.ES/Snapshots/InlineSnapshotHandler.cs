using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Handles inline snapshot creation based on aggregate policies.
/// </summary>
/// <remarks>
/// <para>
/// This handler is invoked after events are committed to evaluate whether
/// a snapshot should be created based on the aggregate's policy.
/// </para>
/// <para>
/// Snapshot creation is synchronous and adds latency to the commit operation.
/// Failures are logged but do not fail the commit - the events are already persisted.
/// </para>
/// </remarks>
public class InlineSnapshotHandler : IInlineSnapshotHandler
{
    private readonly ISnapShotStore _snapshotStore;
    private readonly ISnapshotPolicyProvider _policyProvider;
    private readonly ILogger<InlineSnapshotHandler>? _logger;
    private readonly SnapshotOptions _options;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.Snapshots");

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineSnapshotHandler"/> class.
    /// </summary>
    /// <param name="snapshotStore">The snapshot store for persisting snapshots.</param>
    /// <param name="policyProvider">The policy provider for determining snapshot triggers.</param>
    /// <param name="options">The snapshot options.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public InlineSnapshotHandler(
        ISnapShotStore snapshotStore,
        ISnapshotPolicyProvider policyProvider,
        IOptions<SnapshotOptions> options,
        ILogger<InlineSnapshotHandler>? logger = null)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _options = options?.Value ?? SnapshotOptions.Default;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InlineSnapshotHandler"/> class.
    /// </summary>
    /// <param name="snapshotStore">The snapshot store for persisting snapshots.</param>
    /// <param name="policyProvider">The policy provider for determining snapshot triggers.</param>
    /// <param name="options">The snapshot options.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public InlineSnapshotHandler(
        ISnapShotStore snapshotStore,
        ISnapshotPolicyProvider policyProvider,
        SnapshotOptions options,
        ILogger<InlineSnapshotHandler>? logger = null)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _options = options ?? SnapshotOptions.Default;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SnapshotResult> HandlePostCommitAsync(
        Aggregate aggregate,
        IObjectDocument document,
        IReadOnlyList<JsonEvent> committedEvents,
        JsonTypeInfo jsonTypeInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(committedEvents);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        using var activity = ActivitySource.StartActivity("InlineSnapshotHandler.HandlePostCommit");
        var aggregateType = aggregate.GetType();
        activity?.SetTag("AggregateType", aggregateType.Name);

        // Get policy for this aggregate type
        var policy = _policyProvider.GetPolicy(aggregateType);
        if (policy is null || !policy.Enabled)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("No snapshot policy for {AggregateType}", aggregateType.Name);
            }

            return SnapshotResult.Skipped("No policy configured");
        }

        // Aggregate implements ISnapshotTracker - use concrete type for performance
        Aggregate tracker = aggregate;

        // Determine event type of last committed event
        Type? lastEventType = null;
        if (committedEvents.Count > 0)
        {
            // Try to resolve event type from registry or use the event type name
            lastEventType = ResolveEventType();
        }

        // Update tracker with committed events
        tracker.RecordEventsAppended(committedEvents.Count);

        // Check if snapshot should be created
        var currentVersion = document.Active.CurrentStreamVersion;
        var shouldSnapshot = policy.ShouldSnapshot(
            tracker.TotalEventsProcessed,
            tracker.EventsSinceLastSnapshot,
            lastEventType);

        if (!shouldSnapshot)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug(
                    "Snapshot not triggered for {StreamId} at version {Version}. " +
                    "Events since last: {EventsSince}, Total: {Total}",
                    document.Active.StreamIdentifier,
                    currentVersion,
                    tracker.EventsSinceLastSnapshot,
                    tracker.TotalEventsProcessed);
            }

            return SnapshotResult.Skipped("Policy conditions not met");
        }

        // Create snapshot
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.Timeout);

            var stopwatch = Stopwatch.StartNew();
            await _snapshotStore.SetAsync(
                aggregate,
                jsonTypeInfo,
                document,
                currentVersion,
                cancellationToken: cts.Token);
            stopwatch.Stop();

            tracker.RecordSnapshotCreated(currentVersion);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Created snapshot for {StreamId} at version {Version} in {Duration}ms",
                    document.Active.StreamIdentifier,
                    currentVersion,
                    stopwatch.ElapsedMilliseconds);
            }

            activity?.SetTag("SnapshotCreated", true);
            activity?.SetTag("SnapshotVersion", currentVersion);
            activity?.SetTag("DurationMs", stopwatch.ElapsedMilliseconds);

            return SnapshotResult.Created(currentVersion, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Propagate external cancellation
        }
        catch (OperationCanceledException)
        {
            // Timeout - log but don't fail
            LogSnapshotFailure(
                document.Active.StreamIdentifier,
                currentVersion,
                "Snapshot creation timed out",
                null);

            return SnapshotResult.Failed("Timeout");
        }
        catch (Exception ex)
        {
            // Log but don't fail - events are already committed
            LogSnapshotFailure(
                document.Active.StreamIdentifier,
                currentVersion,
                "Snapshot creation failed",
                ex);

            return SnapshotResult.Failed(ex.Message);
        }
    }

    private void LogSnapshotFailure(string streamId, int version, string message, Exception? ex)
    {
        if (_options.LogFailuresAsWarnings)
        {
            if (_logger?.IsEnabled(LogLevel.Warning) == true)
            {
                _logger.LogWarning(ex,
                    "{Message} for {StreamId} at version {Version}. Events are committed.",
                    message, streamId, version);
            }
        }
        else
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug(ex,
                    "{Message} for {StreamId} at version {Version}. Events are committed.",
                    message, streamId, version);
            }
        }
    }

    private static Type? ResolveEventType()
    {
        // Simple type resolution - in production this would use the event registry
        // For now, return null and rely on policy.OnEvents containing concrete types
        return null;
    }
}

/// <summary>
/// Interface for inline snapshot handling.
/// </summary>
public interface IInlineSnapshotHandler
{
    /// <summary>
    /// Handles snapshot creation after events are committed.
    /// </summary>
    /// <param name="aggregate">The aggregate that events were committed to.</param>
    /// <param name="document">The object document for the stream.</param>
    /// <param name="committedEvents">The events that were committed.</param>
    /// <param name="jsonTypeInfo">JSON type info for serialization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the snapshot operation.</returns>
    Task<SnapshotResult> HandlePostCommitAsync(
        Aggregate aggregate,
        IObjectDocument document,
        IReadOnlyList<JsonEvent> committedEvents,
        JsonTypeInfo jsonTypeInfo,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a snapshot operation.
/// </summary>
public record SnapshotResult
{
    /// <summary>
    /// Gets whether the snapshot operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets whether a snapshot was created.
    /// </summary>
    public bool SnapshotCreated { get; init; }

    /// <summary>
    /// Gets the version at which the snapshot was created, if any.
    /// </summary>
    public int? Version { get; init; }

    /// <summary>
    /// Gets the duration of the snapshot operation, if created.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets the reason for the result (e.g., why skipped or error message).
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Creates a result indicating the snapshot was skipped.
    /// </summary>
    public static SnapshotResult Skipped(string reason) => new()
    {
        Success = true,
        SnapshotCreated = false,
        Reason = reason
    };

    /// <summary>
    /// Creates a result indicating the snapshot was created.
    /// </summary>
    public static SnapshotResult Created(int version, TimeSpan duration) => new()
    {
        Success = true,
        SnapshotCreated = true,
        Version = version,
        Duration = duration
    };

    /// <summary>
    /// Creates a result indicating the snapshot operation failed.
    /// </summary>
    public static SnapshotResult Failed(string error) => new()
    {
        Success = false,
        SnapshotCreated = false,
        Reason = error
    };
}
