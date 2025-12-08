using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ErikLieben.FA.ES.CLI.Commands;

/// <summary>
/// Full-screen TUI display for watch mode with real-time updates.
/// Shows file activity, regeneration status, entity statistics, and logs.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Console TUI rendering code - tested manually through integration")]
public sealed class WatchDisplay : IWatchDisplay
{
    private readonly object _lock = new();
    private readonly string _solutionPath;

    // State
    private WatchStatus _status = WatchStatus.Initializing;
    private DateTime _startTime = DateTime.UtcNow;
    private DateTime? _lastRegenTime;
    private long _lastRegenDurationMs;
    private int _fullRegenCount;
    private int _incrementalRegenCount;
    private int _totalFilesWatched;
    private int _totalEntitiesCached;
    private string? _currentOperation;

    // Progress tracking for initial analysis
    private int _analysisProgress;
    private int _analysisTotal = 100;
    private string _analysisMessage = "Initializing...";

    // Recent activity (circular buffer)
    private readonly List<ActivityEntry> _recentActivity = [];
    private const int MaxActivityEntries = 15;

    // Entity stats
    private int _aggregateCount;
    private int _projectionCount;
    private int _inheritedAggregateCount;
    private int _eventCount;

    // Live display
    private LiveDisplayContext? _liveContext;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    // Animation
    private int _animationFrame;
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private static readonly string[] WatchingFrames = ["◐", "◓", "◑", "◒"];

    // Actions
    public event Action? OnFullRegenRequested;
    public event Action? OnClearActivityRequested;

    public WatchDisplay(string solutionPath, bool verbose = false)
    {
        _solutionPath = solutionPath;
        _ = verbose; // Parameter kept for API compatibility
    }

    public enum WatchStatus
    {
        Initializing,
        Watching,
        Regenerating,
        Error,
        Stopped
    }

    public record ActivityEntry(DateTime Time, ActivityType Type, string Message);

    public enum ActivityType
    {
        FileChanged,
        FileCreated,
        FileDeleted,
        RegenStarted,
        RegenCompleted,
        RegenFailed,
        Info,
        Warning,
        Error,
        ChangeAdded,
        ChangeRemoved,
        ChangeModified
    }

    public async Task RunAsync(Func<Task> watchLoop, CancellationToken cancellationToken)
    {
        _startTime = DateTime.UtcNow;
        _status = WatchStatus.Watching;

        // Clear screen to take over the terminal
        AnsiConsole.Clear();

        await AnsiConsole.Live(BuildLayout())
            .AutoClear(true)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Bottom)
            .StartAsync(async ctx =>
            {
                _liveContext = ctx;

                // Start refresh task
                _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _refreshTask = RefreshLoopAsync(_refreshCts.Token);

                // Start keyboard listener task
                var keyboardTask = KeyboardListenerAsync(_refreshCts.Token);

                try
                {
                    await watchLoop();
                }
                finally
                {
                    if (_refreshCts != null)
                    {
                        await _refreshCts.CancelAsync();
                    }
                    if (_refreshTask != null)
                    {
                        try { await _refreshTask; } catch { /* Expected during cancellation */ }
                    }
                    try { await keyboardTask; } catch { /* Expected during cancellation */ }
                }
            });
    }

    private async Task KeyboardListenerAsync(CancellationToken ct)
    {
        await Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        HandleKeyPress(key);
                    }
                    else
                    {
                        Thread.Sleep(50); // Small delay to prevent busy-waiting
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore keyboard errors (e.g., when console is redirected)
                    break;
                }
            }
        }, ct);
    }

    private void HandleKeyPress(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.F:
                // Full regeneration
                LogActivity(ActivityType.Info, "Full re-generation requested by user");
                OnFullRegenRequested?.Invoke();
                break;

            case ConsoleKey.C when key.Modifiers != ConsoleModifiers.Control:
                // Clear activity log (but not Ctrl+C which exits)
                lock (_lock)
                {
                    _recentActivity.Clear();
                }
                LogActivity(ActivityType.Info, "Activity log cleared");
                OnClearActivityRequested?.Invoke();
                break;
        }
    }

    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Refresh();
                await Task.Delay(100, ct); // 10 FPS refresh
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Refresh()
    {
        lock (_lock)
        {
            _animationFrame = (_animationFrame + 1) % 100; // Cycle through frames
            _liveContext?.UpdateTarget(BuildLayout());
        }
    }

    private Layout BuildLayout()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(1),
                new Layout("Main").SplitColumns(
                    new Layout("Left").Size(35).SplitRows(
                        new Layout("Status").Size(8),
                        new Layout("Chart").Size(5),
                        new Layout("Stats")
                    ),
                    new Layout("Activity")
                ),
                new Layout("Footer").Size(3)
            );

        layout["Header"].Update(BuildHeader());
        layout["Status"].Update(BuildStatusPanel());
        layout["Chart"].Update(BuildEntityChart());
        layout["Stats"].Update(BuildStatsPanel());
        layout["Activity"].Update(BuildActivityPanel());
        layout["Footer"].Update(BuildFooter());

        return layout;
    }

    private Rule BuildHeader()
    {
        var solutionName = Path.GetFileName(_solutionPath);

        // Use Rule widget for a clean header without box width issues
        var title =
            "[grey]EL.FA.ES[/]  " +
            ":high_voltage: [yellow bold]WATCH MODE[/]  " +
            $"[orchid]{Markup.Escape(solutionName)}[/]";

        return new Rule(title)
            .RuleStyle("dim")
            .Centered();
    }

    private Panel BuildStatusPanel()
    {
        var statusColor = _status switch
        {
            WatchStatus.Initializing => "blue",
            WatchStatus.Watching => "green",
            WatchStatus.Regenerating => "yellow",
            WatchStatus.Error => "red",
            WatchStatus.Stopped => "gray",
            _ => "white"
        };

        var statusIcon = _status switch
        {
            WatchStatus.Initializing => SpinnerFrames[_animationFrame % SpinnerFrames.Length],
            WatchStatus.Watching => WatchingFrames[_animationFrame / 3 % WatchingFrames.Length],
            WatchStatus.Regenerating => SpinnerFrames[_animationFrame % SpinnerFrames.Length],
            WatchStatus.Error => "✗",
            WatchStatus.Stopped => "○",
            _ => "?"
        };

        var statusText = _status switch
        {
            WatchStatus.Initializing => "Initializing...",
            WatchStatus.Watching => "Watching for changes",
            WatchStatus.Regenerating => _currentOperation ?? "Regenerating...",
            WatchStatus.Error => "Error occurred",
            WatchStatus.Stopped => "Stopped",
            _ => "Unknown"
        };

        var uptime = DateTime.UtcNow - _startTime;
        var uptimeStr = FormatUptime(uptime);

        var lastRegenStr = _lastRegenTime.HasValue
            ? $"{GetRelativeTime(_lastRegenTime.Value)} ({FormatDuration(_lastRegenDurationMs)})"
            : "Never";

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("")
            .AddColumn("");

        table.AddRow(
            new Markup($"[{statusColor}]{statusIcon}[/] [{statusColor} bold]{statusText}[/]"),
            new Text("")
        );

        // Show progress bar during initialization
        if (_status == WatchStatus.Initializing)
        {
            var progressPct = _analysisTotal > 0 ? (double)_analysisProgress / _analysisTotal : 0;
            var barWidth = 20;
            var filledWidth = (int)(progressPct * barWidth);
            var emptyWidth = barWidth - filledWidth;
            var progressBar = $"[green]{new string('█', filledWidth)}[/][dim]{new string('░', emptyWidth)}[/]";
            var pctStr = $"{progressPct * 100:F0}%";

            table.AddRow(
                new Markup(progressBar),
                new Markup($"[dim]{pctStr}[/]")
            );
            table.AddRow(
                new Markup($"[dim]{Markup.Escape(TruncateMessage(_analysisMessage, 25))}[/]"),
                new Text("")
            );
        }
        else
        {
            table.AddRow(
                new Markup("[dim]Uptime:[/]"),
                new Markup($"[white]{uptimeStr}[/]")
            );
            table.AddRow(
                new Markup("[dim]Last regen:[/]"),
                new Markup($"[white]{lastRegenStr}[/]")
            );
        }

        return new Panel(table)
            .Header("[bold]Status[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(1, 0);
    }

    private Panel BuildEntityChart()
    {
        var total = _aggregateCount + _projectionCount + _inheritedAggregateCount;

        if (total == 0)
        {
            return new Panel(new Markup("[dim italic]No entities yet[/]"))
                .Header("[bold]Entities[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey)
                .Padding(0, 0)
                .Expand();
        }

        // Build a simple bar chart using block characters
        var barWidth = 25;
        var aggPct = (double)_aggregateCount / total;
        var inhPct = (double)_inheritedAggregateCount / total;

        var aggBars = (int)(aggPct * barWidth);
        var inhBars = (int)(inhPct * barWidth);
        var projBars = Math.Max(0, barWidth - aggBars - inhBars);

        var bar = $"[green]{new string('█', aggBars)}[/][cyan1]{new string('█', inhBars)}[/][yellow]{new string('█', projBars)}[/]";

        var content = new Rows(
            new Markup(bar),
            new Markup($"[green]■[/] A:{_aggregateCount} [cyan1]■[/] I:{_inheritedAggregateCount} [yellow]■[/] P:{_projectionCount}")
        );

        return new Panel(content)
            .Header("[bold]Entities[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(0, 0)
            .Expand();
    }

    private Panel BuildStatsPanel()
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("")
            .AddColumn(new TableColumn("").RightAligned());

        // Entity counts with icons
        table.AddRow(
            new Markup("[green]◆[/] Aggregates"),
            new Markup($"[bold]{_aggregateCount}[/]")
        );
        table.AddRow(
            new Markup("[cyan1]◈[/] Inherited Aggregates"),
            new Markup($"[bold]{_inheritedAggregateCount}[/]")
        );
        table.AddRow(
            new Markup("[yellow]◇[/] Projections"),
            new Markup($"[bold]{_projectionCount}[/]")
        );
        table.AddRow(
            new Markup("[magenta]◉[/] Events"),
            new Markup($"[bold]{_eventCount}[/]")
        );

        table.AddEmptyRow();

        // Regeneration stats
        table.AddRow(
            new Markup("[dim]Full regens:[/]"),
            new Markup($"[blue]{_fullRegenCount}[/]")
        );
        table.AddRow(
            new Markup("[dim]Incremental:[/]"),
            new Markup($"[blue]{_incrementalRegenCount}[/]")
        );

        table.AddEmptyRow();

        // File stats
        table.AddRow(
            new Markup("[dim]Files watched:[/]"),
            new Markup($"[gray]{_totalFilesWatched}[/]")
        );
        table.AddRow(
            new Markup("[dim]Entities cached:[/]"),
            new Markup($"[gray]{_totalEntitiesCached}[/]")
        );

        return new Panel(table)
            .Header("[bold]Statistics[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(1, 0)
            .Expand();
    }

    private Panel BuildActivityPanel()
    {
        // Calculate available width for messages based on console width
        // Console width - left panel (35) - borders/padding (~10) - time column (8) - icon column (3) - spacing
        var consoleWidth = Math.Max(Console.WindowWidth, 80);
        var leftPanelWidth = 35;
        var activityPanelWidth = consoleWidth - leftPanelWidth;
        var messageColumnWidth = Math.Max(30, activityPanelWidth - 20); // 20 for time, icon, borders, padding

        var table = new Table()
            .Border(TableBorder.None)
            .Expand()
            .HideHeaders()
            .AddColumn(new TableColumn("Time").Width(8).NoWrap())
            .AddColumn(new TableColumn("Icon").Width(2).NoWrap())
            .AddColumn(new TableColumn("Message").NoWrap());

        List<ActivityEntry> entries;
        lock (_lock)
        {
            entries = _recentActivity.ToList();
        }

        if (entries.Count == 0)
        {
            table.AddRow(
                new Text(""),
                new Text(""),
                new Markup("[dim italic]Waiting for file changes...[/]")
            );
        }
        else
        {
            var visibleEntries = entries.AsEnumerable().Reverse().Take(MaxActivityEntries).ToList();
            var totalVisible = visibleEntries.Count;
            var index = 0;

            foreach (var entry in visibleEntries)
            {
                var (icon, baseColor) = entry.Type switch
                {
                    ActivityType.FileChanged => ("●", Color.Blue),
                    ActivityType.FileCreated => ("+", Color.Green),
                    ActivityType.FileDeleted => ("-", Color.Red),
                    ActivityType.RegenStarted => ("⟳", Color.Yellow),
                    ActivityType.RegenCompleted => ("✓", Color.Blue),
                    ActivityType.RegenFailed => ("✗", Color.Red),
                    ActivityType.Info => ("ℹ", Color.White),
                    ActivityType.Warning => ("⚠", Color.Yellow),
                    ActivityType.Error => ("✗", Color.Red),
                    ActivityType.ChangeAdded => ("+", Color.Green),
                    ActivityType.ChangeRemoved => ("-", Color.Red),
                    ActivityType.ChangeModified => ("~", Color.Orange1),
                    _ => ("·", Color.Grey)
                };

                // Calculate age-based opacity (newer = brighter, older = dimmer)
                var age = DateTime.UtcNow - entry.Time;
                var ageSeconds = age.TotalSeconds;
                var opacity = ageSeconds switch
                {
                    < 2 => 1.0f,    // Very recent - full brightness
                    < 10 => 0.9f,   // Recent
                    < 30 => 0.7f,   // Moderate
                    < 60 => 0.5f,   // Older
                    _ => 0.35f      // Old - dim
                };

                // Also fade based on position in list (top = newest = brightest)
                var positionFade = 1.0f - (index / (float)Math.Max(totalVisible, 1)) * 0.3f;
                opacity *= positionFade;

                var timeStr = entry.Time.ToString("HH:mm:ss");

                // Apply dim styling for older entries
                var dimPrefix = opacity < 0.6f ? "dim " : "";

                // Truncate message based on available width
                var truncatedMessage = TruncateMessage(entry.Message, messageColumnWidth);

                table.AddRow(
                    new Markup($"[grey]{timeStr}[/]"),
                    new Markup($"[{dimPrefix}{baseColor.ToMarkup()}]{icon}[/]"),
                    new Markup($"[{dimPrefix}{baseColor.ToMarkup()}]{Markup.Escape(truncatedMessage)}[/]")
                );

                index++;
            }
        }

        return new Panel(table)
            .Header("[bold]Recent Activity[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(1, 0)
            .Expand();
    }

    private static Panel BuildFooter()
    {
        var shortcuts = new Markup(
            "[yellow]F[/] [dim]Full re-generate[/]  " +
            "[yellow]C[/] [dim]Clear log[/]  " +
            "[yellow]Ctrl+C[/] [dim]Exit[/]"
        );

        return new Panel(Align.Center(shortcuts))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(0, 0);
    }

    private static string GetRelativeTime(DateTime time)
    {
        var diff = DateTime.UtcNow - time;

        if (diff.TotalSeconds < 5) return "just now";
        if (diff.TotalSeconds < 60) return $"{(int)diff.TotalSeconds}s ago";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        return $"{(int)diff.TotalHours}h ago";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1)
        {
            return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }

        if (uptime.TotalMinutes >= 1)
        {
            return $"{uptime.Minutes}m {uptime.Seconds}s";
        }

        return $"{uptime.Seconds}s";
    }

    private static string FormatDuration(long milliseconds)
    {
        if (milliseconds < 1000)
            return $"{milliseconds}ms";
        if (milliseconds < 60000)
            return $"{milliseconds / 1000.0:F1}s";
        var minutes = milliseconds / 60000;
        var seconds = (milliseconds % 60000) / 1000;
        return $"{minutes}m {seconds}s";
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (message.Length <= maxLength) return message;
        return message[..(maxLength - 3)] + "...";
    }

    // Public methods to update state

    public void SetStatus(WatchStatus status, string? operation = null)
    {
        lock (_lock)
        {
            _status = status;
            _currentOperation = operation;
        }
    }

    public void SetAnalysisProgress(int current, int total, string message)
    {
        lock (_lock)
        {
            _analysisProgress = current;
            _analysisTotal = Math.Max(total, 1);
            _analysisMessage = message;
        }
    }

    public void LogAnalysisProgress(string message)
    {
        LogActivity(ActivityType.Info, message);
    }

    public void SetEntityCounts(int aggregates, int projections, int inherited, int events = 0)
    {
        lock (_lock)
        {
            _aggregateCount = aggregates;
            _projectionCount = projections;
            _inheritedAggregateCount = inherited;
            _eventCount = events;
        }
    }

    public void SetFileCounts(int filesWatched, int entitiesCached)
    {
        lock (_lock)
        {
            _totalFilesWatched = filesWatched;
            _totalEntitiesCached = entitiesCached;
        }
    }

    public void LogActivity(ActivityType type, string message)
    {
        lock (_lock)
        {
            _recentActivity.Add(new ActivityEntry(DateTime.UtcNow, type, message));

            // Keep only recent entries
            while (_recentActivity.Count > MaxActivityEntries * 2)
            {
                _recentActivity.RemoveAt(0);
            }
        }
    }

    public void LogFileChange(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        LogActivity(ActivityType.FileChanged, $"Modified: {fileName}");
    }

    public void LogFileCreated(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        LogActivity(ActivityType.FileCreated, $"Created: {fileName}");
    }

    public void LogFileDeleted(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        LogActivity(ActivityType.FileDeleted, $"Deleted: {fileName}");
    }

    public void LogRegenStarted(bool isIncremental, int entityCount = 0)
    {
        var msg = isIncremental
            ? $"Incremental regeneration ({entityCount} entities)"
            : "Full regeneration started";
        LogActivity(ActivityType.RegenStarted, msg);
        SetStatus(WatchStatus.Regenerating, msg);
    }

    public void LogRegenCompleted(bool isIncremental, long elapsedMs)
    {
        lock (_lock)
        {
            _lastRegenTime = DateTime.UtcNow;
            _lastRegenDurationMs = elapsedMs;
            if (isIncremental)
                _incrementalRegenCount++;
            else
                _fullRegenCount++;
        }

        var msg = isIncremental
            ? $"Incremental complete ({FormatDuration(elapsedMs)})"
            : $"Full regeneration complete ({FormatDuration(elapsedMs)})";
        LogActivity(ActivityType.RegenCompleted, msg);
        SetStatus(WatchStatus.Watching);
    }

    public void LogRegenFailed(string error)
    {
        LogActivity(ActivityType.RegenFailed, $"Failed: {error}");
        SetStatus(WatchStatus.Error, error);

        // Auto-recover to watching state after a delay
        Task.Run(async () =>
        {
            await Task.Delay(3000);
            lock (_lock)
            {
                if (_status == WatchStatus.Error)
                {
                    _status = WatchStatus.Watching;
                }
            }
        });
    }

    public void LogEntityRegenerated(string entityType, string entityName)
    {
        LogActivity(ActivityType.Info, $"Regenerated {entityType}: {entityName}");
    }

    public void LogChange(Abstractions.DetectedChange change)
    {
        var activityType = change.Type switch
        {
            Abstractions.ChangeType.Added => ActivityType.ChangeAdded,
            Abstractions.ChangeType.Removed => ActivityType.ChangeRemoved,
            Abstractions.ChangeType.Modified => ActivityType.ChangeModified,
            _ => ActivityType.Info
        };

        var message = change.Details != null
            ? $"{change.Description} ({change.Details})"
            : change.Description;

        LogActivity(activityType, message);
    }

    public void LogChanges(IReadOnlyList<Abstractions.DetectedChange> changes, bool isInitial = false)
    {
        if (changes.Count == 0) return;

        // For initial analysis, just show a summary
        if (isInitial)
        {
            var aggregates = changes.Count(c => c.Category == Abstractions.ChangeCategory.Aggregate);
            var projections = changes.Count(c => c.Category == Abstractions.ChangeCategory.Projection);
            var inherited = changes.Count(c => c.Category == Abstractions.ChangeCategory.InheritedAggregate);

            if (aggregates > 0 || projections > 0 || inherited > 0)
            {
                var parts = new List<string>();
                if (aggregates > 0) parts.Add($"{aggregates} aggregates");
                if (projections > 0) parts.Add($"{projections} projections");
                if (inherited > 0) parts.Add($"{inherited} inherited");
                LogActivity(ActivityType.Info, $"Found {string.Join(", ", parts)}");
            }
            return;
        }

        // For incremental updates, show detailed changes (limit to most recent)
        var changesToShow = changes.Take(10).ToList();
        foreach (var change in changesToShow)
        {
            LogChange(change);
        }

        if (changes.Count > 10)
        {
            LogActivity(ActivityType.Info, $"... and {changes.Count - 10} more changes");
        }
    }

    public void Dispose()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }
}
