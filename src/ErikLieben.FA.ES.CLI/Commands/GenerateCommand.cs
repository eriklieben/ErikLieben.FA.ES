using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Generation;
using ErikLieben.FA.ES.CLI.IO;
using ErikLieben.FA.ES.CLI.Logging;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.Win32;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ErikLieben.FA.ES.CLI.Commands;

[ExcludeFromCodeCoverage(Justification = "CLI command orchestration with console I/O, file system, and process launching")]
public partial class GenerateCommand : AsyncCommand<GenerateCommand.Settings>
{
    private static readonly JsonSerializerOptions AnalyzeJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[Path]")] public string? Path { get; set; }

        [CommandOption("-d|--with-diff")] public bool WithDiff { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Resolve solution path
        var solutionPathResult = ResolveSolutionPath(settings);
        if (!solutionPathResult.Success)
        {
            return 1;
        }

        var fullPath = Path.GetFullPath(settings.Path!);
        var folderPath = Path.GetDirectoryName(fullPath)!;
        var solutionName = Path.GetFileName(fullPath);

        // Header
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[grey]EL.FA.ES[/]  [yellow bold]GENERATE[/]  [orchid]{Markup.Escape(solutionName)}[/]").RuleStyle("dim").Centered());
        AnsiConsole.WriteLine();

        // Load configuration
        var config = await LoadConfigAsync(folderPath, cancellationToken);

        // Create logger and code writer using new architecture
        var logger = new ConsoleActivityLogger(AnsiConsole.Console, verbose: false);
        var codeWriter = new FileSystemCodeWriter(logger, skipUnchanged: true);
        var performanceTracker = new PerformanceTracker();

        // Create orchestrator with all generators
        var orchestrator = GenerationOrchestrator.CreateDefault(
            logger,
            codeWriter,
            config,
            performanceTracker: performanceTracker,
            parallelGeneration: false); // Sequential for progress display

        // Run generation
        var result = await orchestrator.GenerateAsync(settings.Path!, cancellationToken);

        // Temp for testing
        await Setup.Setup.Initialize(folderPath);

        // Display summary
        DisplayAnalysisSummary(result.Solution);

        // Save analyzed data
        var analyzeDir = Path.Combine(folderPath, ".elfa");
        var (analyzePath, hasChanges) = await SaveAnalyzeDataWithBackupAsync(analyzeDir, result.Solution, cancellationToken);

        // Show diff if requested and changes exist
        if (hasChanges && settings.WithDiff)
        {
            await ShowDiffInVSCodeIfNeededAsync(analyzePath, cancellationToken);
        }

        stopwatch.Stop();

        // Final summary
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Generation Complete[/]").RuleStyle("green dim"));

        _ = performanceTracker.GetMetrics(); // Metrics collected for future use
        AnsiConsole.MarkupLine($"[dim]Analysis:[/] [white]{result.AnalysisDuration.TotalSeconds:F2}s[/]");
        AnsiConsole.MarkupLine($"[dim]Generation:[/] [white]{result.GenerationDuration.TotalSeconds:F2}s[/]");
        AnsiConsole.MarkupLine($"[dim]Files:[/] [white]{result.GeneratedFiles.Count}[/] ([green]{result.GeneratedFiles.Count - result.FilesSkipped} written[/], [dim]{result.FilesSkipped} unchanged[/])");
        AnsiConsole.MarkupLine($"[dim]Total time:[/] [white]{stopwatch.Elapsed.TotalSeconds:F2}s[/]");
        AnsiConsole.WriteLine();

        return 0;
    }

    private static void DisplayAnalysisSummary(SolutionDefinition def)
    {
        var aggregateCount = def.Projects.Sum(p => p.Aggregates.Count);
        var inheritedCount = def.Projects.Sum(p => p.InheritedAggregates.Count);
        var projectionCount = def.Projects.Sum(p => p.Projections.Count);
        var eventCount = def.Projects.Sum(p =>
            p.Aggregates.Sum(a => a.Events.Count) + p.Projections.Sum(pr => pr.Events.Count));

        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Entity Type[/]").Centered())
            .AddColumn(new TableColumn("[bold]Count[/]").Centered());

        table.AddRow("[green]Aggregates[/]", $"[bold]{aggregateCount}[/]");
        table.AddRow("[cyan1]Inherited Aggregates[/]", $"[bold]{inheritedCount}[/]");
        table.AddRow("[yellow]Projections[/]", $"[bold]{projectionCount}[/]");
        table.AddRow("[magenta]Events[/]", $"[bold]{eventCount}[/]");

        AnsiConsole.Write(Align.Center(table));
    }

    private static (bool Success, string? Path) ResolveSolutionPath(Settings settings)
    {
#if DEBUG
        // Allow unit tests to skip the hardcoded debug path by setting ELFA_SKIP_DEBUG_PATH=1
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
                Console.WriteLine("No .sln or .slnx file was supplied and no file was found in the current directory or its subdirectories.");
                return (false, null);
            }

            Console.WriteLine();
            AnsiConsole.MarkupLine($"Auto-detected solution file: [orchid]{settings.Path}[/]");
        }

        return (true, settings.Path);
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

    private static async Task<(string AnalyzePath, bool HasChanges)> SaveAnalyzeDataWithBackupAsync(
        string analyzeDir,
        object def,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(analyzeDir);
        var analyzePath = Path.Combine(analyzeDir, "eriklieben.fa.es.analyzed-data.json");
        var newJsonDef = JsonSerializer.Serialize(def, AnalyzeJsonOptions);
        bool hasChanges = false;

        if (File.Exists(analyzePath))
        {
            var existingJsonDef = await File.ReadAllTextAsync(analyzePath, cancellationToken);
            if (!existingJsonDef.Equals(newJsonDef))
            {
                File.Move(analyzePath, $"{analyzePath}.bak.json", true);
                hasChanges = true;
            }
        }

        AnsiConsole.MarkupLine($"Saving analyze data to: [gray62]{analyzePath}[/]");
        await File.WriteAllTextAsync(analyzePath, newJsonDef, cancellationToken);

        return (analyzePath, hasChanges);
    }

    private static async Task ShowDiffInVSCodeIfNeededAsync(string analyzePath, CancellationToken cancellationToken)
    {
        var vscodeExecutable = FindInCommonLocations();
        if (string.IsNullOrEmpty(vscodeExecutable))
        {
            AnsiConsole.MarkupLine("[yellow]Unable to locate VS Code. Skipping diff view.[/]");
            return;
        }

        string arguments = $"--new-window --diff {analyzePath}.bak.json {analyzePath}";
        using var process = new Process();
        process.StartInfo.FileName = vscodeExecutable;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        Console.WriteLine("Starting Visual Studio Code...");
        process.OutputDataReceived += (_, e) => AnsiConsole.MarkupLine($"[gray62]{e.Data}[/]");
        process.Start();

        // Wait for the process to exit
        Console.WriteLine("Waiting for Visual Studio Code to close...");
        await process.WaitForExitAsync(cancellationToken);
    }


    private static string? FindSolutionFile()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var slnFiles = Directory.GetFiles(currentDirectory, "*.sln", SearchOption.AllDirectories);
        var slnxFiles = Directory.GetFiles(currentDirectory, "*.slnx", SearchOption.AllDirectories);

        var allSolutionFiles = slnFiles.Concat(slnxFiles).ToArray();

        if (allSolutionFiles.Length > 0)
        {
            return allSolutionFiles[0];
        }

        return null;
    }

    private static string? FindInCommonLocations()
    {
        // Common locations for user and system installations
        string[] commonPaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe"),
            @"C:\\Program Files\\Microsoft VS Code\\Code.exe",
            @"C:\\Program Files (x86)\\Microsoft VS Code\\Code.exe"
        ];

        return commonPaths.FirstOrDefault(p => File.Exists(p));
    }

}
