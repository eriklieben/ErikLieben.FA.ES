#pragma warning disable S3776 // Cognitive Complexity - file watching and code generation coordination requires complex control flow
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
    private readonly IChangeDetector _changeDetector = new ChangeDetector();

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

            // Find which entities are affected by the changed files
            var affectedEntities = FindAffectedEntities(changedFiles);

            if (affectedEntities.Count == 0)
            {
                // No tracked entities affected - might be a new file, do full regen
                if (_display != null)
                {
                    _display.LogActivity(WatchDisplay.ActivityType.Warning, "Untracked file changed, running full regen");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]New or untracked file changed, running full regeneration...[/]");
                }
                await RegenerateFullAsync(solutionPath, folderPath, config, cancellationToken);
                return;
            }

            // Check if Extensions file needs regeneration (new aggregate/projection added)
            bool needsExtensionsRegen = affectedEntities.Any(e =>
                e.StartsWith("Aggregate:", StringComparison.Ordinal) ||
                e.StartsWith("Projection:", StringComparison.Ordinal));

            if (_display != null)
            {
                _display.LogRegenStarted(isIncremental: true, affectedEntities.Count);
            }
            else
            {
                AnsiConsole.MarkupLine($"[blue]Incremental regeneration[/] [gray]({affectedEntities.Count} entities)[/]");
            }

            // Re-analyze the solution to get updated definitions
            // This is needed to detect what actually changed in the source files
            var analyzer = new Analyze.Analyze(config);
            (var newSolution, string resolvedSolutionPath) = await analyzer.AnalyzeAsync(solutionPath);

            // Detect and log changes
            var changes = _changeDetector.DetectChanges(_cachedSolution, newSolution);
            if (_display != null && changes.Count > 0)
            {
                _display.LogChanges(changes, isInitial: false);
            }

            // Update cache with new solution
            _cachedSolution = newSolution;
            _cachedSolutionPath = resolvedSolutionPath;
            BuildFileToEntityMap(newSolution, resolvedSolutionPath);

            // Update entity counts in display
            if (_display != null)
            {
                var aggregateCount = newSolution.Projects.Sum(p => p.Aggregates.Count);
                var projectionCount = newSolution.Projects.Sum(p => p.Projections.Count);
                var inheritedCount = newSolution.Projects.Sum(p => p.InheritedAggregates.Count);
                var eventCount = newSolution.Projects.Sum(p =>
                    p.Aggregates.Sum(a => a.Events.Count) + p.Projections.Sum(pr => pr.Events.Count));
                _display.SetEntityCounts(aggregateCount, projectionCount, inheritedCount, eventCount);
            }

            // Regenerate affected entities with the NEW definitions
            foreach (var entity in affectedEntities)
            {
                await RegenerateEntityAsync(entity, config, cancellationToken);
            }

            // Regenerate extensions if needed (fast - just registration code)
            if (needsExtensionsRegen)
            {
                await new GenerateExtensionCode(_cachedSolution, config, _cachedSolutionPath!).Generate();
            }

            stopwatch.Stop();
            _lastRegeneration = DateTime.UtcNow;

            if (_display != null)
            {
                _display.LogRegenCompleted(isIncremental: true, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓ Incremental regeneration complete[/] [gray]({stopwatch.ElapsedMilliseconds}ms)[/]");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (_display != null)
            {
                _display.LogRegenFailed(ex.Message);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Regeneration failed[/] [gray]({stopwatch.ElapsedMilliseconds}ms)[/]");
                AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            }

            // On error, clear cache to force full regen next time
            _cachedSolution = null;
        }
    }

    private HashSet<string> FindAffectedEntities(HashSet<string> changedFiles)
    {
        var affected = new HashSet<string>();

        foreach (var file in changedFiles)
        {
            // Normalize path for lookup
            var normalizedFile = file.Replace('/', '\\');

            if (_fileToEntityMap.TryGetValue(normalizedFile, out var entities))
            {
                foreach (var entity in entities)
                {
                    affected.Add(entity);
                }
            }
            else
            {
                // Try partial match (file might be stored with relative path)
                foreach (var kvp in _fileToEntityMap)
                {
                    if (normalizedFile.EndsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.EndsWith(Path.GetFileName(normalizedFile), StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var entity in kvp.Value)
                        {
                            affected.Add(entity);
                        }
                    }
                }
            }
        }

        return affected;
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
                var aggregateWithProject = _cachedSolution.Projects
                    .SelectMany(p => p.Aggregates.Select(a => (Project: p, Aggregate: a)))
                    .FirstOrDefault(x => x.Aggregate.IdentifierName == entityName);
                if (aggregateWithProject.Aggregate != null)
                {
                    if (_display != null)
                    {
                        _display.LogEntityRegenerated("aggregate", entityName);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  [gray]→[/] Regenerating aggregate: [green]{entityName}[/]");
                    }
                    await GenerateSingleAggregate(aggregateWithProject.Project, aggregateWithProject.Aggregate, _cachedSolutionPath, config);
                }
                break;

            case "Projection":
                var projectionWithProject = _cachedSolution.Projects
                    .SelectMany(p => p.Projections.Select(proj => (Project: p, Projection: proj)))
                    .FirstOrDefault(x => x.Projection.Name == entityName);
                if (projectionWithProject.Projection != null)
                {
                    if (_display != null)
                    {
                        _display.LogEntityRegenerated("projection", entityName);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  [gray]→[/] Regenerating projection: [yellow]{entityName}[/]");
                    }
                    await GenerateSingleProjection(projectionWithProject.Project, projectionWithProject.Projection, _cachedSolutionPath, config);
                }
                break;

            case "InheritedAggregate":
                var inheritedWithProject = _cachedSolution.Projects
                    .SelectMany(p => p.InheritedAggregates.Select(ia => (Project: p, Inherited: ia)))
                    .FirstOrDefault(x => x.Inherited.IdentifierName == entityName);
                if (inheritedWithProject.Inherited != null)
                {
                    if (_display != null)
                    {
                        _display.LogEntityRegenerated("inherited", entityName);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  [gray]→[/] Regenerating inherited aggregate: [green]{entityName}[/]");
                    }
                    await GenerateSingleInheritedAggregate(inheritedWithProject.Project, inheritedWithProject.Inherited, _cachedSolutionPath, config);
                }
                break;
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
            if (_display != null && !isTuiMode)
            {
                _display.LogRegenStarted(isIncremental: false);
            }
            else if (_display == null)
            {
                AnsiConsole.MarkupLine($"[blue]Full regeneration...[/]");
            }

            // Analyze solution - use silent mode when in TUI
            Analyze.Analyze analyzer;
            if (isTuiMode && _display != null)
            {
                _display.LogActivity(WatchDisplay.ActivityType.Info, "Starting solution analysis...");
                analyzer = new Analyze.Analyze(config);
                string? lastProjectName = null;
                analyzer.OnProgress = (current, total, message) =>
                {
                    _display.SetAnalysisProgress(current, total, message);

                    // Log when we start analyzing a new project
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
            }
            else if (_display != null)
            {
                // Regeneration while TUI is running - use silent mode
                analyzer = new Analyze.Analyze(config);
            }
            else
            {
                // Simple mode - use progress bar
                analyzer = new Analyze.Analyze(config, AnsiConsole.Console);
            }

            (var def, string resolvedSolutionPath) = await analyzer.AnalyzeAsync(solutionPath);

            // Detect changes between previous and current solution
            // Use _cachedSolution (the last successfully analyzed solution) as the baseline
            var isInitialAnalysis = _cachedSolution == null;
            var changes = _changeDetector.DetectChanges(_cachedSolution, def);

            // Cache for incremental updates
            // _cachedSolution becomes the new baseline for next comparison
            _cachedSolution = def;
            _cachedSolutionPath = resolvedSolutionPath;
            BuildFileToEntityMap(def, resolvedSolutionPath);

            // Update display with entity counts and detected changes
            if (_display != null)
            {
                var aggregateCount = def.Projects.Sum(p => p.Aggregates.Count);
                var projectionCount = def.Projects.Sum(p => p.Projections.Count);
                var inheritedCount = def.Projects.Sum(p => p.InheritedAggregates.Count);
                var eventCount = def.Projects.Sum(p =>
                    p.Aggregates.Sum(a => a.Events.Count) + p.Projections.Sum(pr => pr.Events.Count));
                _display.SetEntityCounts(aggregateCount, projectionCount, inheritedCount, eventCount);
                _display.SetFileCounts(_fileToEntityMap.Count, aggregateCount + projectionCount + inheritedCount);

                // Log detected changes
                if (!isInitialAnalysis && changes.Count > 0)
                {
                    _display.LogChanges(changes, isInitial: false);
                }

                if (isTuiMode)
                {
                    if (isInitialAnalysis)
                    {
                        _display.LogActivity(WatchDisplay.ActivityType.Info,
                            $"Found {aggregateCount} aggregates, {projectionCount} projections");
                    }
                    _display.SetStatus(WatchDisplay.WatchStatus.Initializing, "Generating code...");
                    _display.LogActivity(WatchDisplay.ActivityType.Info, "Generating code...");
                }
            }

            // Save analyzed data
            var analyzeDir = Path.Combine(folderPath, ".elfa");
            Directory.CreateDirectory(analyzeDir);
            var analyzePath = Path.Combine(analyzeDir, "eriklieben.fa.es.analyzed-data.json");
            var newJsonDef = JsonSerializer.Serialize(def, AnalyzeJsonOptions);
            await File.WriteAllTextAsync(analyzePath, newJsonDef, cancellationToken);

            // Generate code
            await new GenerateAggregateCode(def, config, resolvedSolutionPath).Generate();
            await new GenerateProjectionCode(def, config, resolvedSolutionPath).Generate();
            await new GenerateInheritedAggregateCode(def, config, resolvedSolutionPath).Generate();
            await new GenerateExtensionCode(def, config, resolvedSolutionPath).Generate();
            await new GenerateVersionTokenOfTCode(def, config, resolvedSolutionPath).Generate();
            await new GenerateVersionTokenOfTJsonConverterCode(def, config, resolvedSolutionPath).Generate();

            stopwatch.Stop();
            _lastRegeneration = DateTime.UtcNow;

            var entityCount = def.Projects.Sum(p => p.Aggregates.Count + p.Projections.Count);
            if (_display != null)
            {
                _display.LogRegenCompleted(isIncremental: false, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓ Full regeneration complete[/] [gray]({stopwatch.ElapsedMilliseconds}ms, {entityCount} entities cached)[/]");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (_display != null)
            {
                _display.LogRegenFailed(ex.Message);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Full regeneration failed[/] [gray]({stopwatch.ElapsedMilliseconds}ms)[/]");
                AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
            }
            _cachedSolution = null;
        }
    }

    private void BuildFileToEntityMap(SolutionDefinition solution, string solutionPath)
    {
        _fileToEntityMap.Clear();

        foreach (var project in solution.Projects)
        {
            foreach (var aggregate in project.Aggregates)
            {
                foreach (var fileLocation in aggregate.FileLocations)
                {
                    var fullPath = Path.Combine(solutionPath, fileLocation.Replace('/', '\\'));
                    var normalizedPath = Path.GetFullPath(fullPath);

                    if (!_fileToEntityMap.TryGetValue(normalizedPath, out var entities))
                    {
                        entities = [];
                        _fileToEntityMap[normalizedPath] = entities;
                    }
                    entities.Add($"Aggregate:{aggregate.IdentifierName}");
                }
            }

            foreach (var projection in project.Projections)
            {
                foreach (var fileLocation in projection.FileLocations)
                {
                    var fullPath = Path.Combine(solutionPath, fileLocation.Replace('/', '\\'));
                    var normalizedPath = Path.GetFullPath(fullPath);

                    if (!_fileToEntityMap.TryGetValue(normalizedPath, out var entities))
                    {
                        entities = [];
                        _fileToEntityMap[normalizedPath] = entities;
                    }
                    entities.Add($"Projection:{projection.Name}");
                }
            }

            foreach (var inherited in project.InheritedAggregates)
            {
                foreach (var fileLocation in inherited.FileLocations)
                {
                    var fullPath = Path.Combine(solutionPath, fileLocation.Replace('/', '\\'));
                    var normalizedPath = Path.GetFullPath(fullPath);

                    if (!_fileToEntityMap.TryGetValue(normalizedPath, out var entities))
                    {
                        entities = [];
                        _fileToEntityMap[normalizedPath] = entities;
                    }
                    entities.Add($"InheritedAggregate:{inherited.IdentifierName}");
                }
            }
        }

        if (_display == null)
        {
            AnsiConsole.MarkupLine($"[gray]Mapped {_fileToEntityMap.Count} source files to entities[/]");
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
