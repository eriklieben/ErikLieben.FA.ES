namespace ErikLieben.FA.ES.Retention;

/// <summary>
/// Service for discovering and processing streams exceeding retention policies.
/// </summary>
public interface IRetentionDiscoveryService
{
    /// <summary>
    /// Discovers streams exceeding their retention policies.
    /// </summary>
    /// <param name="options">Discovery options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of retention violations.</returns>
    IAsyncEnumerable<RetentionViolation> DiscoverViolationsAsync(
        RetentionDiscoveryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a single retention violation using the configured action.
    /// </summary>
    /// <param name="violation">The violation to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processing result.</returns>
    Task<RetentionProcessingResult> ProcessViolationAsync(
        RetentionViolation violation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes multiple retention violations.
    /// </summary>
    /// <param name="violations">The violations to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processing results.</returns>
    IAsyncEnumerable<RetentionProcessingResult> ProcessViolationsAsync(
        IEnumerable<RetentionViolation> violations,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for retention violation discovery.
/// </summary>
public record RetentionDiscoveryOptions
{
    /// <summary>
    /// Gets the aggregate types to check. If null, checks all types with policies.
    /// </summary>
    public IEnumerable<string>? AggregateTypes { get; init; }

    /// <summary>
    /// Gets the maximum results to return.
    /// </summary>
    public int MaxResults { get; init; } = 1000;

    /// <summary>
    /// Gets the continuation token for pagination.
    /// </summary>
    public string? ContinuationToken { get; init; }
}

/// <summary>
/// Represents a retention policy violation.
/// </summary>
/// <param name="StreamId">The stream identifier.</param>
/// <param name="ObjectName">The aggregate/object type name.</param>
/// <param name="Policy">The retention policy that was violated.</param>
/// <param name="CurrentEventCount">Current number of events in the stream.</param>
/// <param name="OldestEventDate">Date of the oldest event.</param>
/// <param name="ViolationType">The type of violation.</param>
public record RetentionViolation(
    string StreamId,
    string ObjectName,
    RetentionPolicy Policy,
    int CurrentEventCount,
    DateTimeOffset OldestEventDate,
    RetentionViolationType ViolationType);

/// <summary>
/// Result of processing a retention violation.
/// </summary>
/// <param name="StreamId">The stream identifier.</param>
/// <param name="Success">Whether processing succeeded.</param>
/// <param name="ActionTaken">The retention action that was taken.</param>
/// <param name="NewStreamId">The new stream ID if migrated.</param>
/// <param name="Error">Error message if failed.</param>
public record RetentionProcessingResult(
    string StreamId,
    bool Success,
    Attributes.RetentionAction ActionTaken,
    string? NewStreamId,
    string? Error)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static RetentionProcessingResult Succeeded(
        string streamId,
        Attributes.RetentionAction action,
        string? newStreamId = null)
        => new(streamId, true, action, newStreamId, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static RetentionProcessingResult Failed(
        string streamId,
        Attributes.RetentionAction action,
        string error)
        => new(streamId, false, action, null, error);
}
