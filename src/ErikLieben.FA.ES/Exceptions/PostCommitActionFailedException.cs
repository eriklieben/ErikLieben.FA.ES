using ErikLieben.FA.ES.Actions;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when one or more post-commit actions fail after retries are exhausted.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown after events have been successfully committed to the event store.
/// The committed events are NOT rolled back when this exception is thrown.
/// </para>
/// <para>
/// The exception provides detailed information about which actions failed and which succeeded,
/// allowing consumers to take appropriate recovery actions such as:
/// <list type="bullet">
/// <item>Logging the failure for manual intervention</item>
/// <item>Queuing failed actions for retry</item>
/// <item>Compensating transactions if needed</item>
/// </list>
/// </para>
/// </remarks>
public class PostCommitActionFailedException : EsException
{
    /// <summary>
    /// The error code for post-commit action failures.
    /// </summary>
    public const string PostCommitErrorCode = "ELFAES-POSTCOMMIT-0001";

    /// <summary>
    /// Initializes a new instance of the <see cref="PostCommitActionFailedException"/> class.
    /// </summary>
    /// <param name="streamId">The stream identifier where events were committed.</param>
    /// <param name="committedEvents">The events that were successfully committed.</param>
    /// <param name="failedActions">The actions that failed.</param>
    /// <param name="succeededActions">The actions that succeeded.</param>
    public PostCommitActionFailedException(
        string streamId,
        IReadOnlyList<JsonEvent> committedEvents,
        IReadOnlyList<FailedPostCommitAction> failedActions,
        IReadOnlyList<SucceededPostCommitAction> succeededActions)
        : base(PostCommitErrorCode, BuildMessage(streamId, committedEvents, failedActions, succeededActions))
    {
        ArgumentNullException.ThrowIfNull(streamId);
        ArgumentNullException.ThrowIfNull(committedEvents);
        ArgumentNullException.ThrowIfNull(failedActions);
        ArgumentNullException.ThrowIfNull(succeededActions);

        StreamId = streamId;
        CommittedEvents = committedEvents;
        FailedActions = failedActions;
        SucceededActions = succeededActions;

        if (committedEvents.Count > 0)
        {
            CommittedVersionRange = (committedEvents[0].EventVersion, committedEvents[^1].EventVersion);
        }
    }

    /// <summary>
    /// Gets the stream identifier where events were committed.
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// Gets the events that were successfully committed before the post-commit actions failed.
    /// </summary>
    public IReadOnlyList<JsonEvent> CommittedEvents { get; }

    /// <summary>
    /// Gets the actions that failed to execute.
    /// </summary>
    public IReadOnlyList<FailedPostCommitAction> FailedActions { get; }

    /// <summary>
    /// Gets the actions that executed successfully.
    /// </summary>
    public IReadOnlyList<SucceededPostCommitAction> SucceededActions { get; }

    /// <summary>
    /// Gets the version range of committed events.
    /// </summary>
    public (int FromVersion, int ToVersion) CommittedVersionRange { get; }

    /// <summary>
    /// Gets the names of the failed actions for quick reference.
    /// </summary>
    public IEnumerable<string> FailedActionNames => FailedActions.Select(f => f.ActionName);

    /// <summary>
    /// Gets the names of the succeeded actions for quick reference.
    /// </summary>
    public IEnumerable<string> SucceededActionNames => SucceededActions.Select(s => s.ActionName);

    /// <summary>
    /// Gets the first failure's exception for convenience.
    /// </summary>
    public Exception? FirstError => FailedActions.Count > 0 ? FailedActions[0].Error : null;

    private static string BuildMessage(
        string streamId,
        IReadOnlyList<JsonEvent> committedEvents,
        IReadOnlyList<FailedPostCommitAction> failedActions,
        IReadOnlyList<SucceededPostCommitAction> succeededActions)
    {
        var versionInfo = committedEvents.Count > 0
            ? $" (v{committedEvents[0].EventVersion}-v{committedEvents[^1].EventVersion})"
            : string.Empty;

        return $"Post-commit actions failed for stream {streamId}{versionInfo}. " +
               $"Events ARE committed ({committedEvents.Count} events). " +
               $"Failed: {failedActions.Count}, Succeeded: {succeededActions.Count}. " +
               $"Failed actions: {string.Join(", ", failedActions.Select(f => f.ActionName))}";
    }
}
