using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using Microsoft.Win32;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ErikLieben.FA.ES.CLI.Commands;

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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // temp for testing
#if DEBUG
        // Allow unit tests to skip the hardcoded debug path by setting ELFA_SKIP_DEBUG_PATH=1
        if (Environment.GetEnvironmentVariable("ELFA_SKIP_DEBUG_PATH") != "1" && string.IsNullOrEmpty(settings.Path))
        {
            settings.Path = @"D:\ErikLieben.FA.ES\demo\DemoApp.Solution.sln";
        }
#endif

        if (string.IsNullOrEmpty(settings.Path))
        {
            settings.Path = FindSolutionFile();
            if (settings.Path == null)
            {
                Console.WriteLine("No .sln file was supplied and no file was found in the current directory or its subdirectories.");
                return 1;
            }

            Console.WriteLine();
            AnsiConsole.MarkupLine($"Auto-detected solution file: [orchid]{settings.Path}[/]");

        }

        var fullPath = Path.GetFullPath(settings.Path);
        var folderPath = Path.GetDirectoryName(fullPath);
        // var settingsPath = Path.Combine(folderPath!, "eriklieben-faes.settings.json");

        var config = new Config();
        // if (File.Exists(settingsPath))
        // {
        //     var content = File.ReadAllText(settingsPath);
        //     if (!string.IsNullOrWhiteSpace(content))
        //     {
        //         config = JsonSerializer.Deserialize<Config>(content) ?? new Config();
        //     }
        // }

        // Load config v2
        var path = Path.Combine(folderPath!, ".elfa/config.json");
        if (File.Exists(path))
        {
            var content = await File.ReadAllTextAsync(path);
            if (!string.IsNullOrWhiteSpace(content))
            {
                config = JsonSerializer.Deserialize<Config>(content) ?? new Config();
            }
        }



        var analyzer = new Analyze.Analyze(config, AnsiConsole.Console);
        (var def, string solutionPath) = await analyzer.AnalyzeAsync(settings.Path);

        // Temp for testing
        await new Setup.Setup().Initialize(solutionPath);



        // Save the analysed data
        var analyzeDir = Path.Combine(folderPath!, ".elfa\\");
        Directory.CreateDirectory(analyzeDir);
        var analyzePath = Path.Combine(analyzeDir, "eriklieben.fa.es.analyzed-data.json");
        bool existingFile = false;
        bool sameFile = false;
        var newJsonDef = JsonSerializer.Serialize(def, AnalyzeJsonOptions);
        if (File.Exists(analyzePath))
        {
            var existingJsonDef = await File.ReadAllTextAsync(analyzePath);
            if (!existingJsonDef.Equals(newJsonDef))
            {
                File.Move(analyzePath, $"{analyzePath}.bak.json", true);
                existingFile = true;
            }
            else
            {
                sameFile = true;
            }
        }

        AnsiConsole.MarkupLine($"Saving analyze data to: [gray62]{analyzePath}[/]");
        await File.WriteAllTextAsync(analyzePath, newJsonDef);

        if (existingFile && !sameFile && settings.WithDiff)
        {
            var vscodeExecutable = FindInCommonLocations();
            if (!string.IsNullOrEmpty(vscodeExecutable))
            {
                string arguments = $"--new-window --diff {analyzePath}.bak.json {analyzePath}";
                using var process = new Process();
                process.StartInfo.FileName = vscodeExecutable;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                Console.WriteLine("Starting Visual Studio Code...");
                process.OutputDataReceived += (_, e) =>  AnsiConsole.MarkupLine($"[gray62]{e.Data}[/]");
                process.Start();

                // Wait for the process to exit
                Console.WriteLine("Waiting for Visual Studio Code to close...");
                await process.WaitForExitAsync();
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Unable to locate VS Code. Skipping diff view.[/]");
            }
        }

        await new GenerateAggregateCode(def, config, solutionPath).Generate();
        await new GenerateProjectionCode(def, config, solutionPath).Generate();
        await new GenerateInheritedAggregateCode(def, config, solutionPath).Generate();
        await new GenerateExtensionCode(def, config, solutionPath).Generate();
        await new GenerateVersionTokenOfTCode(def, config, solutionPath).Generate();
        await new GenerateVersionTokenOfTJsonConverterCode(def, config, solutionPath).Generate();

        return 0;
    }


    private string? FindSolutionFile()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var slnFiles = Directory.GetFiles(currentDirectory, "*.sln", SearchOption.AllDirectories);

        if (slnFiles.Length > 0)
        {
            return slnFiles[0];
        }

        return null;
    }

    private static string? FindInCommonLocations()
    {
        // Common locations for user and system installations
        string[] commonPaths = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe"),
            @"C:\\Program Files\\Microsoft VS Code\\Code.exe",
            @"C:\\Program Files (x86)\\Microsoft VS Code\\Code.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

}
