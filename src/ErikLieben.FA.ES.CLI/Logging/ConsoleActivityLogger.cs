using System.Collections.Concurrent;
using ErikLieben.FA.ES.CLI.Abstractions;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.Logging;

/// <summary>
/// Activity logger that outputs to Spectre.Console for interactive CLI use.
/// </summary>
public class ConsoleActivityLogger : IActivityLogger
{
    private readonly IAnsiConsole _console;
    private readonly ConcurrentBag<ActivityLogEntry> _entries = [];
    private readonly bool _verbose;

    public ConsoleActivityLogger(IAnsiConsole console, bool verbose = false)
    {
        _console = console;
        _verbose = verbose;
    }

    public event Action<ActivityLogEntry>? OnActivity;

    public void Log(ActivityType type, string message, string? entityType = null, string? entityName = null)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, type, message, entityType, entityName);
        _entries.Add(entry);

        var formattedMessage = FormatMessage(type, message, entityType, entityName);
        if (ShouldOutput(type))
        {
            _console.MarkupLine(formattedMessage);
        }

        OnActivity?.Invoke(entry);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, ActivityType.Error, message, Exception: exception);
        _entries.Add(entry);

        _console.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        if (exception != null && _verbose)
        {
            _console.MarkupLine($"[red dim]{Markup.Escape(exception.ToString())}[/]");
        }

        OnActivity?.Invoke(entry);
    }

    public void LogProgress(int current, int total, string message)
    {
        var entry = new ActivityLogEntry(DateTime.UtcNow, ActivityType.Progress, message);
        _entries.Add(entry);

        if (_verbose)
        {
            var percentage = total > 0 ? (current * 100 / total) : 0;
            _console.MarkupLine($"[dim]{percentage}%[/] {Markup.Escape(message)}");
        }

        OnActivity?.Invoke(entry);
    }

    public IReadOnlyList<ActivityLogEntry> GetActivityLog() =>
        _entries.OrderBy(e => e.Timestamp).ToList().AsReadOnly();

    private bool ShouldOutput(ActivityType type)
    {
        return type switch
        {
            ActivityType.Error => true,
            ActivityType.Warning => true,
            ActivityType.FileGenerated => _verbose,
            ActivityType.FileSkipped => _verbose,
            ActivityType.Info => _verbose,
            ActivityType.AnalysisStarted => true,
            ActivityType.AnalysisCompleted => true,
            ActivityType.GenerationStarted => true,
            ActivityType.GenerationCompleted => true,
            ActivityType.Progress => false, // Progress is handled separately
            _ => _verbose
        };
    }

    private static string FormatMessage(ActivityType type, string message, string? entityType, string? entityName)
    {
        var escapedMessage = Markup.Escape(message);
        var prefix = type switch
        {
            ActivityType.Info => "[dim]",
            ActivityType.Warning => "[yellow]Warning:[/] ",
            ActivityType.Error => "[red]Error:[/] ",
            ActivityType.FileGenerated => "[green]Generated:[/] ",
            ActivityType.FileSkipped => "[dim]Skipped:[/] ",
            ActivityType.AnalysisStarted => "[cyan]",
            ActivityType.AnalysisCompleted => "[green]",
            ActivityType.GenerationStarted => "[cyan]",
            ActivityType.GenerationCompleted => "[green]",
            _ => ""
        };

        var suffix = type switch
        {
            ActivityType.Info => "[/]",
            ActivityType.AnalysisStarted => "[/]",
            ActivityType.AnalysisCompleted => "[/]",
            ActivityType.GenerationStarted => "[/]",
            ActivityType.GenerationCompleted => "[/]",
            _ => ""
        };

        var entityInfo = entityType != null && entityName != null
            ? $" [dim]({entityType}: {Markup.Escape(entityName)})[/]"
            : "";

        return $"{prefix}{escapedMessage}{suffix}{entityInfo}";
    }
}
