namespace ErikLieben.FA.ES.Retention;

/// <summary>
/// Configuration options for retention policies.
/// </summary>
public class RetentionOptions
{
    /// <summary>
    /// Gets or sets the default retention policy applied to aggregates without explicit policies.
    /// </summary>
    public RetentionPolicy? DefaultPolicy { get; set; }

    /// <summary>
    /// Gets the policy overrides by aggregate type name.
    /// </summary>
    public Dictionary<string, RetentionPolicy> PolicyOverrides { get; } = new();

    /// <summary>
    /// Gets or sets whether to automatically run retention discovery on startup.
    /// </summary>
    public bool AutoDiscoverOnStartup { get; set; }

    /// <summary>
    /// Gets or sets the batch size for processing retention violations.
    /// </summary>
    public int ProcessingBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum concurrent violations to process.
    /// </summary>
    public int MaxConcurrentProcessing { get; set; } = 5;

    /// <summary>
    /// Gets the default options instance.
    /// </summary>
    public static RetentionOptions Default { get; } = new();
}
