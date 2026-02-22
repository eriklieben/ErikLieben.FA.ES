using ErikLieben.FA.ES.Attributes;

namespace ErikLieben.FA.ES.Retention;

/// <summary>
/// Runtime retention policy for an aggregate type.
/// </summary>
public class RetentionPolicy
{
    /// <summary>
    /// Gets or sets the maximum age of the stream.
    /// </summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of events.
    /// </summary>
    public int MaxEvents { get; set; }

    /// <summary>
    /// Gets or sets the action to take when limits are exceeded.
    /// </summary>
    public RetentionAction Action { get; set; } = RetentionAction.Migrate;

    /// <summary>
    /// Gets or sets whether this policy is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of recent events to keep when migrating.
    /// </summary>
    public int KeepRecentEvents { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to create a summary event when migrating.
    /// </summary>
    public bool CreateSummaryOnMigration { get; set; } = true;

    /// <summary>
    /// Checks if a stream violates this retention policy.
    /// </summary>
    /// <param name="eventCount">The total event count in the stream.</param>
    /// <param name="oldestEventDate">The date of the oldest event.</param>
    /// <returns>The violation type, or null if no violation.</returns>
    public RetentionViolationType? CheckViolation(int eventCount, DateTimeOffset oldestEventDate)
    {
        if (!Enabled)
            return null;

        var exceedsAge = MaxAge.HasValue && DateTimeOffset.UtcNow - oldestEventDate > MaxAge.Value;
        var exceedsCount = MaxEvents > 0 && eventCount > MaxEvents;

        return (exceedsAge, exceedsCount) switch
        {
            (true, true) => RetentionViolationType.Both,
            (true, false) => RetentionViolationType.ExceedsMaxAge,
            (false, true) => RetentionViolationType.ExceedsMaxEvents,
            _ => null
        };
    }

    /// <summary>
    /// Creates a <see cref="RetentionPolicy"/> from a <see cref="RetentionPolicyAttribute"/>.
    /// </summary>
    public static RetentionPolicy FromAttribute(RetentionPolicyAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        return new RetentionPolicy
        {
            MaxAge = ParseDuration(attribute.MaxAge),
            MaxEvents = attribute.MaxEvents,
            Action = attribute.Action,
            Enabled = attribute.Enabled,
            KeepRecentEvents = attribute.KeepRecentEvents,
            CreateSummaryOnMigration = attribute.CreateSummaryOnMigration
        };
    }

    /// <summary>
    /// Parses a duration string like "30d", "6m", "1y" into a <see cref="TimeSpan"/>.
    /// </summary>
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
            'm' or 'M' => TimeSpan.FromDays(number * 30),
            'y' or 'Y' => TimeSpan.FromDays(number * 365),
            'h' or 'H' => TimeSpan.FromHours(number),
            _ => throw new ArgumentException($"Invalid duration unit: {unit}. Expected d, w, m, y, or h.")
        };
    }

    /// <summary>
    /// Gets a default policy with no retention limits.
    /// </summary>
    public static RetentionPolicy None { get; } = new() { Enabled = false };
}

/// <summary>
/// Type of retention policy violation.
/// </summary>
public enum RetentionViolationType
{
    /// <summary>Stream exceeds maximum age.</summary>
    ExceedsMaxAge,

    /// <summary>Stream exceeds maximum event count.</summary>
    ExceedsMaxEvents,

    /// <summary>Stream exceeds both age and event count limits.</summary>
    Both
}
