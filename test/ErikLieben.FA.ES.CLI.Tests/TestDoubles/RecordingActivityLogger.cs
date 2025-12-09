using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ErikLieben.FA.ES.CLI.Abstractions;

namespace ErikLieben.FA.ES.CLI.Tests.TestDoubles;

/// <summary>
/// Test double for IActivityLogger that records all activity for verification in tests.
/// </summary>
public class RecordingActivityLogger : IActivityLogger
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
    /// Check if a message was logged
    /// </summary>
    public bool HasMessage(string message) =>
        _entries.Any(e => e.Message.Contains(message, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Get all error messages
    /// </summary>
    public IEnumerable<string> GetErrorMessages() =>
        _entries.Where(e => e.Type == ActivityType.Error).Select(e => e.Message);

    /// <summary>
    /// Get all messages of a specific type
    /// </summary>
    public IEnumerable<string> GetMessages(ActivityType type) =>
        _entries.Where(e => e.Type == type).Select(e => e.Message);

    /// <summary>
    /// Clear all logged entries
    /// </summary>
    public void Clear() => _entries.Clear();
}
