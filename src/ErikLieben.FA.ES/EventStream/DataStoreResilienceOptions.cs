namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Configuration options for the resilient data store wrapper.
/// </summary>
public class DataStoreResilienceOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient failures.
    /// Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay between retry attempts.
    /// Default is 200 milliseconds.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum delay between retry attempts.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value indicating whether to use jitter in retry delays.
    /// Jitter adds randomness to prevent thundering herd problems.
    /// Default is true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to skip wrapping if the underlying
    /// Azure client already has retry configured.
    /// Default is true to avoid double-retry scenarios.
    /// </summary>
    public bool SkipIfClientHasRetry { get; set; } = true;
}
