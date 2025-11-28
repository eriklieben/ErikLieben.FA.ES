using System.Collections.Concurrent;
using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Commands;

namespace ErikLieben.FA.ES.CLI.Logging;

/// <summary>
/// Activity logger that routes activity to the WatchDisplay TUI.
/// </summary>
public class WatchDisplayActivityLogger : IActivityLogger
{
    private readonly WatchDisplay _display;
    private readonly ConcurrentBag<ActivityLogEntry> _entries = [];

    public WatchDisplayActivityLogger(WatchDisplay display)
    {
        _display = display;
    }

    public event Action<ActivityLogEntry>? OnActivity;

    public void Log(ActivityType type, string message, string? entityType = null, string? entityName = null)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, type, message, entityType, entityName);
        _entries.Add(entry);

        // Map to WatchDisplay activity type and log
        var displayType = MapToDisplayActivityType(type);
        _display.LogActivity(displayType, message);

        OnActivity?.Invoke(entry);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, ActivityType.Error, message, Exception: exception);
        _entries.Add(entry);

        var errorMessage = exception != null ? $"{message}: {exception.Message}" : message;
        _display.LogActivity(WatchDisplay.ActivityType.Error, errorMessage);

        OnActivity?.Invoke(entry);
    }

    public void LogProgress(int current, int total, string message)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, ActivityType.Progress, message);
        _entries.Add(entry);

        _display.SetAnalysisProgress(current, total, message);

        OnActivity?.Invoke(entry);
    }

    public IReadOnlyList<ActivityLogEntry> GetActivityLog() =>
        _entries.OrderBy(e => e.Timestamp).ToList().AsReadOnly();

    /// <summary>
    /// Log a file generation event
    /// </summary>
    public void LogFileGenerated(string filePath)
    {
        Log(ActivityType.FileGenerated, $"Generated: {Path.GetFileName(filePath)}");
    }

    /// <summary>
    /// Log a file skip event
    /// </summary>
    public void LogFileSkipped(string filePath)
    {
        Log(ActivityType.FileSkipped, $"Skipped (unchanged): {Path.GetFileName(filePath)}");
    }

    /// <summary>
    /// Set entity counts on the display
    /// </summary>
    public void SetEntityCounts(int aggregates, int projections, int inherited, int events)
    {
        _display.SetEntityCounts(aggregates, projections, inherited, events);
    }

    private static WatchDisplay.ActivityType MapToDisplayActivityType(ActivityType type)
    {
        return type switch
        {
            ActivityType.Info => WatchDisplay.ActivityType.Info,
            ActivityType.Warning => WatchDisplay.ActivityType.Warning,
            ActivityType.Error => WatchDisplay.ActivityType.Error,
            ActivityType.FileGenerated => WatchDisplay.ActivityType.Info,
            ActivityType.FileSkipped => WatchDisplay.ActivityType.Info,
            ActivityType.AnalysisStarted => WatchDisplay.ActivityType.Info,
            ActivityType.AnalysisCompleted => WatchDisplay.ActivityType.RegenCompleted,
            ActivityType.GenerationStarted => WatchDisplay.ActivityType.RegenStarted,
            ActivityType.GenerationCompleted => WatchDisplay.ActivityType.RegenCompleted,
            ActivityType.Progress => WatchDisplay.ActivityType.Info,
            _ => WatchDisplay.ActivityType.Info
        };
    }
}
