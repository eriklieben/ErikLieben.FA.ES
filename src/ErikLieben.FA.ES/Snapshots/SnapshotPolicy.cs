using ErikLieben.FA.ES.Attributes;

namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Runtime snapshot policy for an aggregate type.
/// </summary>
public class SnapshotPolicy
{
    /// <summary>
    /// Gets or sets the event count interval for automatic snapshots.
    /// </summary>
    public int Every { get; set; }

    /// <summary>
    /// Gets the event types that trigger an immediate snapshot.
    /// </summary>
    public HashSet<Type> OnEvents { get; } = [];

    /// <summary>
    /// Gets or sets the maximum number of snapshots to retain.
    /// </summary>
    public int KeepSnapshots { get; set; }

    /// <summary>
    /// Gets or sets the maximum age of snapshots to retain.
    /// </summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets the minimum events required before first snapshot.
    /// </summary>
    public int MinEventsBeforeSnapshot { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether this policy is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Determines if a snapshot should be created based on the current state.
    /// </summary>
    /// <param name="totalEventCount">Total events in the stream.</param>
    /// <param name="eventsSinceLastSnapshot">Events appended since the last snapshot.</param>
    /// <param name="lastAppendedEventType">The type of the last appended event, if any.</param>
    /// <returns>True if a snapshot should be created; otherwise, false.</returns>
    public bool ShouldSnapshot(int totalEventCount, int eventsSinceLastSnapshot, Type? lastAppendedEventType)
    {
        if (!Enabled)
            return false;

        // Check minimum events threshold
        if (totalEventCount < MinEventsBeforeSnapshot)
            return false;

        // Check event type trigger
        if (lastAppendedEventType is not null && OnEvents.Contains(lastAppendedEventType))
            return true;

        // Check event count trigger
        if (Every > 0 && eventsSinceLastSnapshot >= Every)
            return true;

        return false;
    }

    /// <summary>
    /// Creates a <see cref="SnapshotPolicy"/> from a <see cref="SnapshotPolicyAttribute"/>.
    /// </summary>
    /// <param name="attribute">The attribute to convert.</param>
    /// <returns>A new snapshot policy instance.</returns>
    public static SnapshotPolicy FromAttribute(SnapshotPolicyAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        var policy = new SnapshotPolicy
        {
            Every = attribute.Every,
            KeepSnapshots = attribute.KeepSnapshots,
            MaxAge = ParseDuration(attribute.MaxAge),
            MinEventsBeforeSnapshot = attribute.MinEventsBeforeSnapshot,
            Enabled = attribute.Enabled
        };

        if (attribute.OnEvents is not null)
        {
            foreach (var eventType in attribute.OnEvents)
            {
                policy.OnEvents.Add(eventType);
            }
        }

        return policy;
    }

    /// <summary>
    /// Parses a duration string like "30d", "6m", "1y" into a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="duration">The duration string to parse.</param>
    /// <returns>The parsed timespan, or null if the input is null or empty.</returns>
    public static TimeSpan? ParseDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return null;

        var value = duration[..^1];
        var unit = duration[^1];

        if (!int.TryParse(value, out var number))
            throw new ArgumentException($"Invalid duration format: {duration}. Expected format like '30d', '6m', '1y'.");

        return unit switch
        {
            'd' or 'D' => TimeSpan.FromDays(number),
            'w' or 'W' => TimeSpan.FromDays(number * 7),
            'm' or 'M' => TimeSpan.FromDays(number * 30), // Approximate
            'y' or 'Y' => TimeSpan.FromDays(number * 365), // Approximate
            'h' or 'H' => TimeSpan.FromHours(number),
            _ => throw new ArgumentException($"Invalid duration unit: {unit}. Expected d, w, m, y, or h.")
        };
    }

    /// <summary>
    /// Gets a default policy with no automatic snapshots enabled.
    /// </summary>
    public static SnapshotPolicy None { get; } = new() { Enabled = false };
}
