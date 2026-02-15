#pragma warning disable S3267 // Loops should be simplified - explicit loops improve debuggability

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Analysis;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Generation;
using ErikLieben.FA.ES.CLI.IO;
using ErikLieben.FA.ES.CLI.Logging;
using ErikLieben.FA.ES.CLI.Model;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ErikLieben.FA.ES.CLI.Commands;

/// <summary>
/// Watches for file changes and automatically regenerates code when needed.
/// Optimized for incremental regeneration - only regenerates entities whose source files changed.
/// Features a full-screen TUI with real-time status updates.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "CLI command orchestration with file watchers, console I/O, and TUI coordination")]
public class WatchCommand : AsyncCommand<WatchCommand.Settings>
{
    private static readonly JsonSerializerOptions AnalyzeJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    private readonly object _regenerateLock = new();
    private bool _regenerationPending;
    private bool _regenerationRunning;
    private DateTime _lastRegeneration = DateTime.MinValue;
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(500);

    // Cached data for incremental regeneration
    private SolutionDefinition? _cachedSolution;
    private string? _cachedSolutionPath;
    private readonly Dictionary<string, HashSet<string>> _fileToEntityMap = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _pendingChangedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ChangeDetector _changeDetector = new();

    // Deduplication for file change events (Windows fires multiple events per save)
    private readonly Dictionary<string, DateTime> _lastFileChangeLog = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan FileChangeLogDebounce = TimeSpan.FromMilliseconds(500);

    // TUI display
    private WatchDisplay? _display;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[Path]")]
        [Description("Path to the solution file (.sln or .slnx)")]
        public string? Path { get; set; }

        [CommandOption("-v|--verbose")]
        [Description("Show verbose output including all file changes")]
        public bool Verbose { get; set; }

        [CommandOption("--simple")]
        [Description("Use simple output mode without full-screen TUI")]
        public bool Simple { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Resolve solution path
        var solutionPath = ResolveSolutionPath(settings);
        if (solutionPath == null)
        {
            return 1;
        }

        var fullPath = Path.GetFullPath(solutionPath);
        var folderPath = Path.GetDirectoryName(fullPath)!;

        // Load configuration
        var config = await LoadConfigAsync(folderPath, cancellationToken);

        // Use simple mode or full TUI
        if (settings.Simple)
        {
            return await ExecuteSimpleModeAsync(solutionPath, fullPath, folderPath, config, settings, cancellationToken);
        }

        return await ExecuteTuiModeAsync(solutionPath, fullPath, folderPath, config, settings, cancellationToken);
    }

    private async Task<int> ExecuteTuiModeAsync(string solutionPath, string fullPath, string folderPath, Config config, Settings settings, CancellationToken cancellationToken)
    {
        using var display = new WatchDisplay(fullPath, settings.Verbose);
        _display = display;

        // Wire up keyboard shortcuts
        display.OnFullRegenRequested += () =>
        {
            // Clear the cache to force full regeneration
            _cachedSolution = null;
            QueueRegeneration(solutionPath, folderPath, config, null, cancellationToken);
        };

        var watchers = new List<FileSystemWatcher>();

        await display.RunAsync(async () =>
        {
            try
            {
                // Do initial full generation inside the TUI
                display.SetStatus(WatchDisplay.WatchStatus.Initializing, "Analyzing solution...");
                await RegenerateFullAsync(solutionPath, folderPath, config, cancellationToken, isTuiMode: true);

                // Get entity counts for display
                var aggregateCount = _cachedSolution?.Projects.Sum(p => p.Aggregates.Count) ?? 0;
                var projectionCount = _cachedSolution?.Projects.Sum(p => p.Projections.Count) ?? 0;
                var inheritedCount = _cachedSolution?.Projects.Sum(p => p.InheritedAggregates.Count) ?? 0;
                var eventCount = _cachedSolution?.Projects.Sum(p =>
                    p.Aggregates.Sum(a => a.Events.Count) + p.Projections.Sum(pr => pr.Events.Count)) ?? 0;

                display.SetEntityCounts(aggregateCount, projectionCount, inheritedCount, eventCount);
                display.SetFileCounts(_fileToEntityMap.Count, aggregateCount + projectionCount + inheritedCount);
                display.LogActivity(WatchDisplay.ActivityType.Info, "Initial analysis complete");

                // Set up file watchers for all .cs files in the solution directory
                var csWatcher = CreateWatcher(folderPath, "*.cs", settings.Verbose,
                    (changedFile) => QueueRegeneration(solutionPath, folderPath, config, changedFile, cancellationToken));
                watchers.Add(csWatcher);

                // Also watch for new .cs files being added
                csWatcher.Created += (_, e) =>
                {
                    if (ShouldIgnorePath(e.FullPath)) return;
                    _display?.LogFileCreated(e.FullPath);
                    // New file - force full regeneration
                    QueueRegeneration(solutionPath, folderPath, config, null, cancellationToken);
                };

                csWatcher.Deleted += (_, e) =>
                {
                    if (ShouldIgnorePath(e.FullPath)) return;
                    _display?.LogFileDeleted(e.FullPath);
                    // Deleted file - force full regeneration
                    QueueRegeneration(solutionPath, folderPath, config, null, cancellationToken);
                };

                _display?.SetStatus(WatchDisplay.WatchStatus.Watching);

                // Wait indefinitely until cancellation
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _display?.SetStatus(WatchDisplay.WatchStatus.Stopped);
                }
            }
            finally
            {
                foreach (var watcher in watchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
            }
        }, cancellationToken);

        return 0;
    }

    private async Task<int> ExecuteSimpleModeAsync(string solutionPath, string fullPath, string folderPath, Config config, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[green]Watching[/] solution: [orchid]{fullPath}[/]");
        AnsiConsole.MarkupLine("[gray]Press Ctrl+C to stop watching[/]");
        AnsiConsole.WriteLine();

        // Do initial full generation
        await RegenerateFullAsync(solutionPath, folderPath, config, cancellationToken);

        // Set up file watchers for all .cs files in the solution directory
        var watchers = new List<FileSystemWatcher>();

        try
        {
            // Watch for .cs file changes (but not in obj/bin folders)
            var csWatcher = CreateWatcher(folderPath, "*.cs", settings.Verbose,
                (changedFile) => QueueRegeneration(solutionPath, folderPath, config, changedFile, cancellationToken));
            watchers.Add(csWatcher);

            // Also watch for new .cs files being added
            csWatcher.Created += (_, e) =>
            {
                if (ShouldIgnorePath(e.FullPath)) return;
                if (settings.Verbose)
                {
                    AnsiConsole.MarkupLine($"[gray]File created: {e.Name}[/]");
                }
                // New file - force full regeneration
                QueueRegeneration(solutionPath, folderPath, config, null, cancellationToken);
            };

            csWatcher.Deleted += (_, e) =>
            {
                if (ShouldIgnorePath(e.FullPath)) return;
                if (settings.Verbose)
                {
                    AnsiConsole.MarkupLine($"[gray]File deleted: {e.Name}[/]");
                }
                // Deleted file - force full regeneration
                QueueRegeneration(solutionPath, folderPath, config, null, cancellationToken);
            };

            // Wait indefinitely until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Watch stopped.[/]");
            }
        }
        finally
        {
            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }

        return 0;
    }

    private FileSystemWatcher CreateWatcher(string path, string filter, bool verbose, Action<string> onChange)
    {
        var watcher = new FileSystemWatcher(path, filter)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        watcher.Changed += (_, e) =>
        {
            if (ShouldIgnorePath(e.FullPath)) return;

            // Deduplicate log messages (Windows fires multiple events per save)
            var shouldLog = false;
            lock (_lastFileChangeLog)
            {
                if (!_lastFileChangeLog.TryGetValue(e.FullPath, out var lastLog) ||
                    DateTime.UtcNow - lastLog > FileChangeLogDebounce)
                {
                    _lastFileChangeLog[e.FullPath] = DateTime.UtcNow;
                    shouldLog = true;
                }
            }

            if (shouldLog)
            {
                if (_display != null)
                {
                    _display.LogFileChange(e.FullPath);
                }
                else if (verbose)
                {
                    AnsiConsole.MarkupLine($"[gray]File changed: {e.Name}[/]");
                }
            }

            onChange(e.FullPath);
        };

        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private static bool ShouldIgnorePath(string path)
    {
        // Ignore generated files, obj, bin, and .elfa folders
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        return normalizedPath.Contains("/obj/") ||
               normalizedPath.Contains("/bin/") ||
               normalizedPath.Contains("/.elfa/") ||
               normalizedPath.EndsWith(".generated.cs");
    }

    private void QueueRegeneration(string solutionPath, string folderPath, Config config, string? changedFile, CancellationToken cancellationToken)
    {
        lock (_regenerateLock)
        {
            // Track which file changed for incremental regeneration
            if (changedFile != null)
            {
                _pendingChangedFiles.Add(changedFile);
            }

            // If regeneration is pending or running, just queue the file - it will be picked up
            if (_regenerationPending || _regenerationRunning)
            {
                return;
            }

            _regenerationPending = true;
        }

        // Debounce - wait a bit before regenerating to batch multiple rapid changes
        Task.Run(async () =>
        {
            try
            {
                // Wait for debounce interval
                await Task.Delay(DebounceInterval, cancellationToken);

                // Check if enough time has passed since last regeneration
                var timeSinceLastRegen = DateTime.UtcNow - _lastRegeneration;
                if (timeSinceLastRegen < DebounceInterval)
                {
                    await Task.Delay(DebounceInterval - timeSinceLastRegen, cancellationToken);
                }

                // Keep processing while there are pending files
                while (true)
                {
                    HashSet<string> filesToProcess;
                    lock (_regenerateLock)
                    {
                        if (_pendingChangedFiles.Count == 0)
                        {
                            _regenerationPending = false;
                            _regenerationRunning = false;
                            break;
                        }

                        _regenerationRunning = true;
                        filesToProcess = _pendingChangedFiles;
                        _pendingChangedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }

                    await RegenerateIncrementalAsync(solutionPath, folderPath, config, filesToProcess, cancellationToken);

                    // Small delay before checking for more pending files
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled, ignore
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error during regeneration: {ex.Message}[/]");
            }
            finally
            {
                lock (_regenerateLock)
                {
                    _regenerationPending = false;
                    _regenerationRunning = false;
                }
            }
        }, cancellationToken);
    }

    private async Task RegenerateIncrementalAsync(string solutionPath, string folderPath, Config config, HashSet<string> changedFiles, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // If no cache or too many files changed, do full regeneration
            if (_cachedSolution == null || changedFiles.Count == 0 || changedFiles.Count > 10)
            {
                await RegenerateFullAsync(solutionPath, folderPath, config, cancellationToken);
                return;
            }

            var affectedEntities = FindAffectedEntities(changedFiles);

            if (affectedEntities.Count == 0)
            {
                LogMessage(WatchDisplay.ActivityType.Warning, "Untracked file changed, running full regen",
                    "[yellow]New or untracked file changed, running full regeneration...[/]");
                await RegenerateFullAsync(solutionPath, folderPath, config, cancellationToken);
                return;
            }

            bool needsExtensionsRegen = affectedEntities.Any(e =>
                e.StartsWith("Aggregate:", StringComparison.Ordinal) ||
                e.StartsWith("Projection:", StringComparison.Ordinal));

            LogRegenStart(isIncremental: true, affectedEntities.Count);

            await PerformIncrementalRegeneration(solutionPath, config, affectedEntities, needsExtensionsRegen, cancellationToken);

            stopwatch.Stop();
            _lastRegeneration = DateTime.UtcNow;
            LogRegenEnd(isIncremental: true, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogRegenError(ex.Message, stopwatch.ElapsedMilliseconds);
            _cachedSolution = null;
        }
    }

    private async Task PerformIncrementalRegeneration(string solutionPath, Config config, HashSet<string> affectedEntities, bool needsExtensionsRegen, CancellationToken cancellationToken)
    {
        var analyzer = new Analyze.Analyze(config);
        (var newSolution, string resolvedSolutionPath) = await analyzer.AnalyzeAsync(solutionPath);

        var changes = _changeDetector.DetectChanges(_cachedSolution, newSolution);
        if (_display != null && changes.Count > 0)
        {
            _display.LogChanges(changes, isInitial: false);
        }

        _cachedSolution = newSolution;
        _cachedSolutionPath = resolvedSolutionPath;
        BuildFileToEntityMap(newSolution, resolvedSolutionPath);

        UpdateDisplayEntityCounts(newSolution);

        foreach (var entity in affectedEntities)
        {
            await RegenerateEntityAsync(entity, config, cancellationToken);
        }

        if (needsExtensionsRegen)
        {
            await new GenerateExtensionCode(_cachedSolution, config, _cachedSolutionPath!).Generate();
        }
    }

    private void UpdateDisplayEntityCounts(SolutionDefinition solution)
    {
        if (_display == null)
        {
            return;
        }

        var aggregateCount = solution.Projects.Sum(p => p.Aggregates.Count);
        var projectionCount = solution.Projects.Sum(p => p.Projections.Count);
        var inheritedCount = solution.Projects.Sum(p => p.InheritedAggregates.Count);
        var eventCount = solution.Projects.Sum(p =>
            p.Aggregates.Sum(a => a.Events.Count) + p.Projections.Sum(pr => pr.Events.Count));
        _display.SetEntityCounts(aggregateCount, projectionCount, inheritedCount, eventCount);
    }

    private void LogMessage(WatchDisplay.ActivityType type, string tuiMessage, string simpleMessage)
    {
        if (_display != null)
        {
            _display.LogActivity(type, tuiMessage);
        }
        else
        {
            AnsiConsole.MarkupLine(simpleMessage);
        }
    }

    private void LogRegenStart(bool isIncremental, int entityCount = 0)
    {
        if (_display != null)
        {
            _display.LogRegenStarted(isIncremental: isIncremental, entityCount);
        }
        else
        {
            var label = isIncremental ? "Incremental" : "Full";
            AnsiConsole.MarkupLine($"[blue]{label} regeneration[/] [gray]({entityCount} entities)[/]");
        }
    }

    private void LogRegenEnd(bool isIncremental, long elapsedMs)
    {
        if (_display != null)
        {
            _display.LogRegenCompleted(isIncremental: isIncremental, elapsedMs);
        }
        else
        {
            var label = isIncremental ? "Incremental" : "Full";
            AnsiConsole.MarkupLine($"[green]✓ {label} regeneration complete[/] [gray]({elapsedMs}ms)[/]");
        }
    }

    private void LogRegenError(string errorMessage, long elapsedMs)
    {
        if (_display != null)
        {
            _display.LogRegenFailed(errorMessage);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ Regeneration failed[/] [gray]({elapsedMs}ms)[/]");
            AnsiConsole.MarkupLine($"[red]{errorMessage}[/]");
        }
    }

    private HashSet<string> FindAffectedEntities(HashSet<string> changedFiles)
    {
        var affected = new HashSet<string>();

        foreach (var file in changedFiles)
        {
            var normalizedFile = file.Replace('/', '\\');
            AddEntitiesForFile(normalizedFile, affected);
        }

        return affected;
    }

    private void AddEntitiesForFile(string normalizedFile, HashSet<string> affected)
    {
        if (_fileToEntityMap.TryGetValue(normalizedFile, out var entities))
        {
            affected.UnionWith(entities);
            return;
        }

        // Try partial match (file might be stored with relative path)
        var fileName = Path.GetFileName(normalizedFile);
        foreach (var kvp in _fileToEntityMap)
        {
            if (normalizedFile.EndsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            {
                affected.UnionWith(kvp.Value);
            }
        }
    }

    private async Task RegenerateEntityAsync(string entityKey, Config config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_cachedSolution == null || _cachedSolutionPath == null)
            return;

        var parts = entityKey.Split(':', 2);
        if (parts.Length != 2) return;

        var entityType = parts[0];
        var entityName = parts[1];

        switch (entityType)
        {
            case "Aggregate":
                await RegenerateAggregateEntity(entityName, config);
                break;

            case "Projection":
                await RegenerateProjectionEntity(entityName, config);
                break;

            case "InheritedAggregate":
                await RegenerateInheritedAggregateEntity(entityName, config);
                break;
        }
    }

    private async Task RegenerateAggregateEntity(string entityName, Config config)
    {
        var match = _cachedSolution!.Projects
            .SelectMany(p => p.Aggregates.Select(a => (Project: p, Aggregate: a)))
            .FirstOrDefault(x => x.Aggregate.IdentifierName == entityName);

        if (match.Aggregate == null)
            return;

        LogEntityRegen("aggregate", entityName, $"  [gray]->[/] Regenerating aggregate: [green]{entityName}[/]");
        await GenerateSingleAggregate(match.Project, match.Aggregate, _cachedSolutionPath!, config);
    }

    private async Task RegenerateProjectionEntity(string entityName, Config config)
    {
        var match = _cachedSolution!.Projects
            .SelectMany(p => p.Projections.Select(proj => (Project: p, Projection: proj)))
            .FirstOrDefault(x => x.Projection.Name == entityName);

        if (match.Projection == null)
            return;

        LogEntityRegen("projection", entityName, $"  [gray]->[/] Regenerating projection: [yellow]{entityName}[/]");
        await GenerateSingleProjection(match.Project, match.Projection, _cachedSolutionPath!, config);
    }

    private async Task RegenerateInheritedAggregateEntity(string entityName, Config config)
    {
        var match = _cachedSolution!.Projects
            .SelectMany(p => p.InheritedAggregates.Select(ia => (Project: p, Inherited: ia)))
            .FirstOrDefault(x => x.Inherited.IdentifierName == entityName);

        if (match.Inherited == null)
            return;

        LogEntityRegen("inherited", entityName, $"  [gray]->[/] Regenerating inherited aggregate: [green]{entityName}[/]");
        await GenerateSingleInheritedAggregate(match.Project, match.Inherited, _cachedSolutionPath!, config);
    }

    private void LogEntityRegen(string entityType, string entityName, string simpleMessage)
    {
        if (_display != null)
        {
            _display.LogEntityRegenerated(entityType, entityName);
        }
        else
        {
            AnsiConsole.MarkupLine(simpleMessage);
        }
    }

    private static async Task GenerateSingleAggregate(ProjectDefinition project, AggregateDefinition aggregate, string solutionPath, Config config)
    {
        if (!aggregate.IsPartialClass) return;

        // Create a solution with only the affected aggregate (but preserve project metadata)
        var singleAggregateSolution = new SolutionDefinition
        {
            SolutionName = "incremental",
            Projects = [project with { Aggregates = [aggregate], Projections = [], InheritedAggregates = [], VersionTokens = [], VersionTokenJsonConverterDefinitions = [] }]
        };
        var generator = new GenerateAggregateCode(singleAggregateSolution, config, solutionPath);
        await generator.Generate();
    }

    private static async Task GenerateSingleProjection(ProjectDefinition project, ProjectionDefinition projection, string solutionPath, Config config)
    {
        // Create a solution with only the affected projection (but preserve project metadata)
        var singleProjectionSolution = new SolutionDefinition
        {
            SolutionName = "incremental",
            Projects = [project with { Projections = [projection], Aggregates = [], InheritedAggregates = [], VersionTokens = [], VersionTokenJsonConverterDefinitions = [] }]
        };
        var generator = new GenerateProjectionCode(singleProjectionSolution, config, solutionPath);
        await generator.Generate();
    }

    private static async Task GenerateSingleInheritedAggregate(ProjectDefinition project, InheritedAggregateDefinition inherited, string solutionPath, Config config)
    {
        // Create a solution with only the affected inherited aggregate (but preserve project metadata)
        var singleInheritedSolution = new SolutionDefinition
        {
            SolutionName = "incremental",
            Projects = [project with { InheritedAggregates = [inherited], Aggregates = [], Projections = [], VersionTokens = [], VersionTokenJsonConverterDefinitions = [] }]
        };
        var generator = new GenerateInheritedAggregateCode(singleInheritedSolution, config, solutionPath);
        await generator.Generate();
    }

    private async Task RegenerateFullAsync(string solutionPath, string folderPath, Config config, CancellationToken cancellationToken, bool isTuiMode = false)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            LogFullRegenStart(isTuiMode);

            var analyzer = CreateAnalyzer(config, isTuiMode);
            (var def, string resolvedSolutionPath) = await analyzer.AnalyzeAsync(solutionPath);

            var isInitialAnalysis = _cachedSolution == null;
            var changes = _changeDetector.DetectChanges(_cachedSolution, def);

            _cachedSolution = def;
            _cachedSolutionPath = resolvedSolutionPath;
            BuildFileToEntityMap(def, resolvedSolutionPath);

            UpdateDisplayAfterAnalysis(def, changes, isInitialAnalysis, isTuiMode);

            await SaveAnalyzedData(folderPath, def, cancellationToken);
            await RunAllCodeGenerators(def, config, resolvedSolutionPath);

            stopwatch.Stop();
            _lastRegeneration = DateTime.UtcNow;

            LogFullRegenComplete(def, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogRegenError(ex.Message, stopwatch.ElapsedMilliseconds);
            _cachedSolution = null;
        }
    }

    private void LogFullRegenStart(bool isTuiMode)
    {
        if (_display != null && !isTuiMode)
        {
            _display.LogRegenStarted(isIncremental: false);
        }
        else if (_display == null)
        {
            AnsiConsole.MarkupLine($"[blue]Full regeneration...[/]");
        }
    }

    private Analyze.Analyze CreateAnalyzer(Config config, bool isTuiMode)
    {
        if (isTuiMode && _display != null)
        {
            return CreateTuiAnalyzer(config);
        }

        if (_display != null)
        {
            return new Analyze.Analyze(config);
        }

        return new Analyze.Analyze(config, AnsiConsole.Console);
    }

    private Analyze.Analyze CreateTuiAnalyzer(Config config)
    {
        _display!.LogActivity(WatchDisplay.ActivityType.Info, "Starting solution analysis...");
        var analyzer = new Analyze.Analyze(config);
        string? lastProjectName = null;
        analyzer.OnProgress = (current, total, message) =>
        {
            _display.SetAnalysisProgress(current, total, message);

            if (message.StartsWith("Analyzing ") && message.EndsWith("..."))
            {
                var projectName = message["Analyzing ".Length..^3];
                if (projectName != lastProjectName)
                {
                    lastProjectName = projectName;
                    _display.LogAnalysisProgress($"Analyzing: {projectName}");
                }
            }
        };
        return analyzer;
    }

    private void UpdateDisplayAfterAnalysis(SolutionDefinition def, IReadOnlyList<Abstractions.DetectedChange> changes, bool isInitialAnalysis, bool isTuiMode)
    {
        if (_display == null)
        {
            return;
        }

        UpdateDisplayEntityCounts(def);
        _display.SetFileCounts(_fileToEntityMap.Count,
            def.Projects.Sum(p => p.Aggregates.Count + p.Projections.Count + p.InheritedAggregates.Count));

        if (!isInitialAnalysis && changes.Count > 0)
        {
            _display.LogChanges(changes, isInitial: false);
        }

        UpdateDisplayForTuiMode(def, isInitialAnalysis, isTuiMode);
    }

    private void UpdateDisplayForTuiMode(SolutionDefinition def, bool isInitialAnalysis, bool isTuiMode)
    {
        if (!isTuiMode)
        {
            return;
        }

        if (isInitialAnalysis)
        {
            var aggregateCount = def.Projects.Sum(p => p.Aggregates.Count);
            var projectionCount = def.Projects.Sum(p => p.Projections.Count);
            _display!.LogActivity(WatchDisplay.ActivityType.Info,
                $"Found {aggregateCount} aggregates, {projectionCount} projections");
        }
        _display!.SetStatus(WatchDisplay.WatchStatus.Initializing, "Generating code...");
        _display.LogActivity(WatchDisplay.ActivityType.Info, "Generating code...");
    }

    private static async Task SaveAnalyzedData(string folderPath, SolutionDefinition def, CancellationToken cancellationToken)
    {
        var analyzeDir = Path.Combine(folderPath, ".elfa");
        Directory.CreateDirectory(analyzeDir);
        var analyzePath = Path.Combine(analyzeDir, "eriklieben.fa.es.analyzed-data.json");
        var newJsonDef = JsonSerializer.Serialize(def, AnalyzeJsonOptions);
        await File.WriteAllTextAsync(analyzePath, newJsonDef, cancellationToken);
    }

    private static async Task RunAllCodeGenerators(SolutionDefinition def, Config config, string resolvedSolutionPath)
    {
        await new GenerateAggregateCode(def, config, resolvedSolutionPath).Generate();
        await new GenerateProjectionCode(def, config, resolvedSolutionPath).Generate();
        await new GenerateInheritedAggregateCode(def, config, resolvedSolutionPath).Generate();
        await new GenerateExtensionCode(def, config, resolvedSolutionPath).Generate();
        await new GenerateVersionTokenOfTCode(def, config, resolvedSolutionPath).Generate();
        await new GenerateVersionTokenOfTJsonConverterCode(def, config, resolvedSolutionPath).Generate();
    }

    private void LogFullRegenComplete(SolutionDefinition def, long elapsedMs)
    {
        var entityCount = def.Projects.Sum(p => p.Aggregates.Count + p.Projections.Count);
        if (_display != null)
        {
            _display.LogRegenCompleted(isIncremental: false, elapsedMs);
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓ Full regeneration complete[/] [gray]({elapsedMs}ms, {entityCount} entities cached)[/]");
        }
    }

    private void BuildFileToEntityMap(SolutionDefinition solution, string solutionPath)
    {
        _fileToEntityMap.Clear();

        foreach (var project in solution.Projects)
        {
            foreach (var aggregate in project.Aggregates)
            {
                MapFileLocationsToEntity(aggregate.FileLocations, $"Aggregate:{aggregate.IdentifierName}", solutionPath);
            }

            foreach (var projection in project.Projections)
            {
                MapFileLocationsToEntity(projection.FileLocations, $"Projection:{projection.Name}", solutionPath);
            }

            foreach (var inherited in project.InheritedAggregates)
            {
                MapFileLocationsToEntity(inherited.FileLocations, $"InheritedAggregate:{inherited.IdentifierName}", solutionPath);
            }
        }

        if (_display == null)
        {
            AnsiConsole.MarkupLine($"[gray]Mapped {_fileToEntityMap.Count} source files to entities[/]");
        }
    }

    private void MapFileLocationsToEntity(List<string> fileLocations, string entityKey, string solutionPath)
    {
        foreach (var fileLocation in fileLocations)
        {
            var fullPath = Path.Combine(solutionPath, fileLocation.Replace('/', '\\'));
            var normalizedPath = Path.GetFullPath(fullPath);

            if (!_fileToEntityMap.TryGetValue(normalizedPath, out var entities))
            {
                entities = [];
                _fileToEntityMap[normalizedPath] = entities;
            }
            entities.Add(entityKey);
        }
    }

    private static string? ResolveSolutionPath(Settings settings)
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("ELFA_SKIP_DEBUG_PATH") != "1" && string.IsNullOrEmpty(settings.Path))
        {
            settings.Path = @"D:\ErikLieben.FA.ES\demo\TaskFlow.sln";
        }
#endif

        if (string.IsNullOrEmpty(settings.Path))
        {
            settings.Path = FindSolutionFile();
            if (settings.Path == null)
            {
                AnsiConsole.MarkupLine("[red]No .sln or .slnx file was supplied and no file was found in the current directory.[/]");
                return null;
            }

            AnsiConsole.MarkupLine($"Auto-detected solution file: [orchid]{settings.Path}[/]");
        }

        return settings.Path;
    }

    private static async Task<Config> LoadConfigAsync(string folderPath, CancellationToken cancellationToken)
    {
        var config = new Config();
        var path = Path.Combine(folderPath, ".elfa/config.json");

        if (File.Exists(path))
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            if (!string.IsNullOrWhiteSpace(content))
            {
                config = JsonSerializer.Deserialize<Config>(content) ?? new Config();
            }
        }

        return config;
    }

    private static string? FindSolutionFile()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var slnFiles = Directory.GetFiles(currentDirectory, "*.sln", SearchOption.TopDirectoryOnly);
        var slnxFiles = Directory.GetFiles(currentDirectory, "*.slnx", SearchOption.TopDirectoryOnly);

        var allSolutionFiles = slnFiles.Concat(slnxFiles).ToArray();
        return allSolutionFiles.Length > 0 ? allSolutionFiles[0] : null;
    }
}
