namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Defines automatic snapshot policy for an aggregate.
/// </summary>
/// <remarks>
/// <para>
/// Snapshots are created automatically based on the configured policy during event append.
/// This is an inline operation that adds latency to append but guarantees consistency.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [Aggregate]
/// [SnapshotPolicy(Every = 100)]
/// public partial class Order : Aggregate { }
///
/// [Aggregate]
/// [SnapshotPolicy(Every = 50, OnEvents = [typeof(OrderCompleted), typeof(OrderCancelled)])]
/// public partial class Order : Aggregate { }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SnapshotPolicyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the event count interval for automatic snapshots.
    /// A snapshot is created every N events. Set to 0 to disable count-based snapshots.
    /// </summary>
    /// <example>
    /// <code>
    /// [SnapshotPolicy(Every = 100)] // Snapshot every 100 events
    /// </code>
    /// </example>
    public int Every { get; set; }

    /// <summary>
    /// Gets or sets event types that trigger an immediate snapshot.
    /// Useful for business milestones like OrderCompleted, ContractSigned, etc.
    /// </summary>
    /// <example>
    /// <code>
    /// [SnapshotPolicy(OnEvents = [typeof(OrderCompleted), typeof(OrderCancelled)])]
    /// </code>
    /// </example>
    public Type[]? OnEvents { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of snapshots to retain per stream.
    /// Older snapshots are deleted during cleanup. Set to 0 to keep all snapshots.
    /// </summary>
    public int KeepSnapshots { get; set; }

    /// <summary>
    /// Gets or sets the maximum age of snapshots to retain.
    /// Format: "30d" (days), "6m" (months), "1y" (years).
    /// Snapshots older than this are deleted during cleanup.
    /// </summary>
    /// <example>
    /// <code>
    /// [SnapshotPolicy(Every = 100, MaxAge = "90d")] // Keep snapshots for 90 days
    /// </code>
    /// </example>
    public string? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of events required before the first snapshot.
    /// Prevents creating snapshots for short-lived or newly created aggregates.
    /// Default: 10.
    /// </summary>
    public int MinEventsBeforeSnapshot { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether this policy is enabled.
    /// Can be overridden via configuration at runtime.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
