namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Represents the result of executing a post-commit action.
/// </summary>
/// <remarks>
/// Post-commit actions run after events are successfully committed to the event store.
/// This result captures whether the action succeeded or failed, along with diagnostic information.
/// </remarks>
public abstract record PostCommitActionResult(
    string ActionName,
    Type ActionType,
    TimeSpan Duration)
{
    /// <summary>
    /// Creates a successful result for a post-commit action.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="type">The type of the action.</param>
    /// <param name="duration">The time taken to execute the action.</param>
    /// <returns>A successful action result.</returns>
    public static PostCommitActionResult Succeeded(string name, Type type, TimeSpan duration)
        => new SucceededPostCommitAction(name, type, duration);

    /// <summary>
    /// Creates a failed result for a post-commit action.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="type">The type of the action.</param>
    /// <param name="error">The exception that caused the failure.</param>
    /// <param name="attempts">The number of attempts made before failure.</param>
    /// <param name="totalDuration">The total time spent attempting the action.</param>
    /// <returns>A failed action result.</returns>
    public static PostCommitActionResult Failed(string name, Type type, Exception error, int attempts, TimeSpan totalDuration)
        => new FailedPostCommitAction(name, type, error, attempts, totalDuration);

    /// <summary>
    /// Gets a value indicating whether the action succeeded.
    /// </summary>
    public abstract bool IsSuccess { get; }
}

/// <summary>
/// Represents a successful post-commit action execution.
/// </summary>
/// <param name="ActionName">The name of the action.</param>
/// <param name="ActionType">The type of the action.</param>
/// <param name="Duration">The time taken to execute the action.</param>
public record SucceededPostCommitAction(
    string ActionName,
    Type ActionType,
    TimeSpan Duration) : PostCommitActionResult(ActionName, ActionType, Duration)
{
    /// <inheritdoc />
    public override bool IsSuccess => true;
}

/// <summary>
/// Represents a failed post-commit action execution.
/// </summary>
/// <param name="ActionName">The name of the action.</param>
/// <param name="ActionType">The type of the action.</param>
/// <param name="Error">The exception that caused the failure.</param>
/// <param name="RetryAttempts">The number of retry attempts made before failure.</param>
/// <param name="TotalDuration">The total time spent attempting the action.</param>
public record FailedPostCommitAction(
    string ActionName,
    Type ActionType,
    Exception Error,
    int RetryAttempts,
    TimeSpan TotalDuration) : PostCommitActionResult(ActionName, ActionType, TotalDuration)
{
    /// <inheritdoc />
    public override bool IsSuccess => false;
}
