namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Configuration options for post-commit action retry behavior.
/// </summary>
/// <remarks>
/// <para>
/// Post-commit actions run after events are successfully committed to the event store.
/// These options control retry behavior for actions that fail due to transient errors.
/// </para>
/// <para>
/// Important: The events have already been committed when post-commit actions run.
/// Failed actions don't affect the committed events but may need manual intervention
/// or retry via the exception information provided in <see cref="ErikLieben.FA.ES.Exceptions.PostCommitActionFailedException"/>.
/// </para>
/// </remarks>
public class PostCommitRetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts for post-commit actions.
    /// Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay between retry attempts.
    /// Default is 200 milliseconds.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum delay between retry attempts.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the backoff multiplier for exponential delay.
    /// Default is 2.0.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets a value indicating whether to use jitter in retry delays.
    /// Jitter adds randomness to prevent thundering herd problems.
    /// Default is true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Creates a default set of options.
    /// </summary>
    /// <returns>A new instance with default values.</returns>
    public static PostCommitRetryOptions Default => new();

    /// <summary>
    /// Creates options with no retries (fail fast).
    /// </summary>
    /// <returns>A new instance configured for no retries.</returns>
    public static PostCommitRetryOptions NoRetry => new() { MaxRetries = 0 };
}
