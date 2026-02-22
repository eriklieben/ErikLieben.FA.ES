using System.Collections.Concurrent;
using ErikLieben.FA.ES.CLI.Abstractions;

namespace ErikLieben.FA.ES.CLI.Logging;

/// <summary>
/// Activity logger that records all activity without producing console output.
/// Ideal for programmatic use, testing, and Roslyn analyzers.
/// </summary>
public class SilentActivityLogger : IActivityLogger
{
    private readonly ConcurrentBag<ActivityLogEntry> _entries = [];

    public event Action<ActivityLogEntry>? OnActivity;

    public void Log(ActivityType type, string message, string? entityType = null, string? entityName = null)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, type, message, entityType, entityName);
        _entries.Add(entry);
        OnActivity?.Invoke(entry);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, ActivityType.Error, message, Exception: exception);
        _entries.Add(entry);
        OnActivity?.Invoke(entry);
    }

    public void LogProgress(int current, int total, string message)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, ActivityType.Progress, message);
        _entries.Add(entry);
        OnActivity?.Invoke(entry);
    }

    public IReadOnlyList<ActivityLogEntry> GetActivityLog() =>
        _entries.OrderBy(e => e.Timestamp).ToList().AsReadOnly();

    /// <summary>
    /// Check if any errors were logged
    /// </summary>
    public bool HasErrors() => _entries.Any(e => e.Type == ActivityType.Error);

    /// <summary>
    /// Get count of entries by type
    /// </summary>
    public int CountByType(ActivityType type) => _entries.Count(e => e.Type == type);

    /// <summary>
    /// Clear all logged entries
    /// </summary>
    public void Clear() => _entries.Clear();
}
