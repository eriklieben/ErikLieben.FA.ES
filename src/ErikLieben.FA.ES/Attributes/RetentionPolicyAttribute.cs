namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Defines the retention policy for an aggregate's event stream.
/// </summary>
/// <remarks>
/// <para>
/// Retention policies control how long events are kept and what action
/// is taken when limits are exceeded.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [Aggregate]
/// [RetentionPolicy(MaxAge = "365d", MaxEvents = 1000, Action = RetentionAction.Migrate)]
/// public partial class Order : Aggregate { }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RetentionPolicyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the maximum age of the stream before retention action.
    /// Format: "30d" (days), "6m" (months), "1y" (years), "24h" (hours).
    /// </summary>
    /// <example>"365d", "2y", "6m"</example>
    public string? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of events before retention action.
    /// </summary>
    public int MaxEvents { get; set; }

    /// <summary>
    /// Gets or sets the action to take when retention limits are exceeded.
    /// </summary>
    public RetentionAction Action { get; set; } = RetentionAction.Migrate;

    /// <summary>
    /// Gets or sets whether this policy is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of recent events to keep when migrating.
    /// Only applies when <see cref="Action"/> is <see cref="RetentionAction.Migrate"/>.
    /// Default is 100.
    /// </summary>
    public int KeepRecentEvents { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to create a summary event when migrating.
    /// Default is true.
    /// </summary>
    public bool CreateSummaryOnMigration { get; set; } = true;
}

/// <summary>
/// Defines the action to take when retention limits are exceeded.
/// </summary>
public enum RetentionAction
{
    /// <summary>
    /// Migrate to a new stream with summary (preserves state, reduces events).
    /// </summary>
    Migrate = 0,

    /// <summary>
    /// Delete the stream entirely.
    /// </summary>
    Delete = 1,

    /// <summary>
    /// Mark the stream for manual review without taking automatic action.
    /// </summary>
    FlagForReview = 2,

    /// <summary>
    /// Move old events to archive tier (Azure Blob only).
    /// </summary>
    Archive = 3
}
