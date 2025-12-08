using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ErikLieben.FA.ES.CLI.Migration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ErikLieben.FA.ES.CLI.Commands;

/// <summary>
/// Updates ErikLieben.FA.ES packages to the latest version and migrates code for breaking changes.
/// Requires a clean git working directory to ensure safe rollback on failure.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "CLI command orchestration with console I/O, git operations, and package management")]
public class UpdateCommand : AsyncCommand<UpdateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[Path]")]
        [Description("Path to the solution file (.sln or .slnx)")]
        public string? Path { get; set; }

        [CommandOption("-v|--version")]
        [Description("Target version to update to (default: latest)")]
        public string? TargetVersion { get; set; }

        [CommandOption("--skip-git-check")]
        [Description("Skip the git clean working directory check (not recommended)")]
        public bool SkipGitCheck { get; set; }

        [CommandOption("--dry-run")]
        [Description("Show what would be changed without making actual changes")]
        public bool DryRun { get; set; }

        [CommandOption("--skip-generate")]
        [Description("Skip running 'dotnet faes generate' after update")]
        public bool SkipGenerate { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow bold]FAES UPDATE[/]").RuleStyle("dim").Centered());
        AnsiConsole.WriteLine();

        // Step 1: Resolve solution path
        var solutionPath = ResolveSolutionPath(settings);
        if (solutionPath == null)
        {
            return 1;
        }

        var fullPath = System.IO.Path.GetFullPath(solutionPath);
        var folderPath = System.IO.Path.GetDirectoryName(fullPath)!;

        AnsiConsole.MarkupLine($"Solution: [orchid]{fullPath}[/]");
        AnsiConsole.WriteLine();

        // Step 2: Check git status
        if (!settings.SkipGitCheck)
        {
            var gitCheckResult = await CheckGitStatusAsync(folderPath, cancellationToken);
            if (!gitCheckResult.Success)
            {
                return 1;
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Git check skipped - manual recovery may be needed on failure[/]");
            AnsiConsole.WriteLine();
        }

        // Step 3: Detect current version
        var currentVersion = await DetectCurrentVersionAsync(folderPath, cancellationToken);
        if (currentVersion == null)
        {
            AnsiConsole.MarkupLine("[red]Could not detect current ErikLieben.FA.ES version[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Current version: [blue]{currentVersion}[/]");

        // Step 4: Determine target version
        var targetVersion = settings.TargetVersion ?? await GetLatestVersionAsync(cancellationToken);
        if (targetVersion == null)
        {
            AnsiConsole.MarkupLine("[red]Could not determine target version[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Target version:  [green]{targetVersion}[/]");
        AnsiConsole.WriteLine();

        // Check if update is needed
        if (Version.TryParse(currentVersion.TrimStart('v'), out var current) &&
            Version.TryParse(targetVersion.TrimStart('v'), out var target))
        {
            if (current >= target)
            {
                AnsiConsole.MarkupLine("[green]✓ Already at latest version[/]");
                return 0;
            }
        }

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]DRY RUN - No changes will be made[/]");
            AnsiConsole.WriteLine();
        }

        // Step 5: Update NuGet packages
        AnsiConsole.Write(new Rule("[blue]Updating Packages[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        if (!settings.DryRun)
        {
            var updateResult = await UpdatePackagesAsync(folderPath, targetVersion, cancellationToken);
            if (!updateResult)
            {
                AnsiConsole.MarkupLine("[red]✗ Package update failed[/]");
                AnsiConsole.MarkupLine("[yellow]You can restore the previous state with: git checkout .[/]");
                return 1;
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[gray]Would update ErikLieben.FA.ES.* packages to {0}[/]", targetVersion);
        }

        // Step 6: Run code migrations
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[blue]Running Code Migrations[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        var migrator = new CodeMigrator(folderPath, currentVersion, targetVersion, settings.DryRun);
        var migrationResult = await migrator.MigrateAsync(cancellationToken);

        if (!migrationResult.Success)
        {
            AnsiConsole.MarkupLine("[red]✗ Code migration failed[/]");
            AnsiConsole.MarkupLine("[yellow]You can restore the previous state with: git checkout .[/]");
            return 1;
        }

        if (migrationResult.FilesModified > 0)
        {
            AnsiConsole.MarkupLine($"[green]✓ Modified {migrationResult.FilesModified} file(s)[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[gray]No code migrations needed[/]");
        }

        // Step 7: Run generate
        if (!settings.SkipGenerate && !settings.DryRun)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[blue]Regenerating Code[/]").RuleStyle("dim"));
            AnsiConsole.WriteLine();

            var generateResult = await RunGenerateAsync(solutionPath, cancellationToken);
            if (!generateResult)
            {
                AnsiConsole.MarkupLine("[red]✗ Code generation failed[/]");
                AnsiConsole.MarkupLine("[yellow]You can restore the previous state with: git checkout .[/]");
                return 1;
            }
        }

        // Summary
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Update Complete[/]").RuleStyle("green dim"));
        AnsiConsole.WriteLine();

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]This was a dry run. Run without --dry-run to apply changes.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓ Successfully updated from {currentVersion} to {targetVersion}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Review the changes and commit when ready:[/]");
            AnsiConsole.MarkupLine("[gray]  git add .[/]");
            AnsiConsole.MarkupLine($"[gray]  git commit -m \"chore: update ErikLieben.FA.ES to {targetVersion}\"[/]");
        }

        AnsiConsole.WriteLine();

        return 0;
    }

    private static async Task<(bool Success, bool IsGitRepo, bool IsClean)> CheckGitStatusAsync(string folderPath, CancellationToken cancellationToken)
    {
        // Check if it's a git repository
        var gitCheckProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --is-inside-work-tree",
                WorkingDirectory = folderPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            gitCheckProcess.Start();
            await gitCheckProcess.WaitForExitAsync(cancellationToken);

            if (gitCheckProcess.ExitCode != 0)
            {
                AnsiConsole.MarkupLine("[red]✗ Not a git repository[/]");
                AnsiConsole.MarkupLine("[yellow]The update command requires git to ensure safe rollback on failure.[/]");
                AnsiConsole.MarkupLine("[gray]Initialize a git repository with: git init[/]");
                return (false, false, false);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Git check failed: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[yellow]Make sure git is installed and available in PATH.[/]");
            return (false, false, false);
        }

        AnsiConsole.MarkupLine("[green]✓ Git repository detected[/]");

        // Check if working directory is clean
        var statusProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "status --porcelain",
                WorkingDirectory = folderPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        statusProcess.Start();
        var statusOutput = await statusProcess.StandardOutput.ReadToEndAsync(cancellationToken);
        await statusProcess.WaitForExitAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(statusOutput))
        {
            AnsiConsole.MarkupLine("[red]✗ Working directory has uncommitted changes[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Please commit or stash your changes before running update.[/]");
            AnsiConsole.MarkupLine("[gray]This ensures you can easily rollback if the update fails.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Uncommitted changes:[/]");

            var lines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Take(10))
            {
                AnsiConsole.MarkupLine($"[gray]  {Markup.Escape(line)}[/]");
            }
            if (lines.Length > 10)
            {
                AnsiConsole.MarkupLine($"[gray]  ... and {lines.Length - 10} more[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Use --skip-git-check to bypass this check (not recommended)[/]");
            return (false, true, false);
        }

        AnsiConsole.MarkupLine("[green]✓ Working directory is clean[/]");
        AnsiConsole.WriteLine();

        return (true, true, true);
    }

    private static async Task<string?> DetectCurrentVersionAsync(string folderPath, CancellationToken cancellationToken)
    {
        // Find any .csproj file with ErikLieben.FA.ES reference
        var csprojFiles = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories);

        foreach (var csproj in csprojFiles)
        {
            var content = await File.ReadAllTextAsync(csproj, cancellationToken);

            // Match PackageReference for ErikLieben.FA.ES packages
            var match = Regex.Match(content, @"<PackageReference\s+Include=""ErikLieben\.FA\.ES""[^>]*Version=""([^""]+)""", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Also check the closing tag format
            match = Regex.Match(content, @"<PackageReference\s+Include=""ErikLieben\.FA\.ES[^""]*""\s*>\s*<Version>([^<]+)</Version>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static async Task<string?> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        // Query NuGet for latest version
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "package search ErikLieben.FA.ES --take 1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            // Parse version from output - format varies, look for version pattern
            var match = Regex.Match(output, @"ErikLieben\.FA\.ES\s+(\d+\.\d+\.\d+(?:-\w+)?)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Fallback: use hardcoded latest if NuGet query fails
            return "2.0.0";
        }
        catch
        {
            // If NuGet query fails, use hardcoded version
            return "2.0.0";
        }
    }

    private static async Task<bool> UpdatePackagesAsync(string folderPath, string targetVersion, CancellationToken cancellationToken)
    {
        // Find all .csproj files
        var csprojFiles = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}") &&
                       !f.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}"))
            .ToList();

        var packagesToUpdate = new[]
        {
            "ErikLieben.FA.ES",
            "ErikLieben.FA.ES.Analyzers",
            "ErikLieben.FA.ES.AzureStorage",
            "ErikLieben.FA.ES.Testing",
            "ErikLieben.FA.ES.Azure.Functions.Worker.Extensions",
            "ErikLieben.FA.ES.WebJobs.Isolated.Extensions",
            "ErikLieben.FA.ES.AspNetCore.MinimalApis",
            "ErikLieben.FA.ES.EventStreamManagement"
        };

        foreach (var csproj in csprojFiles)
        {
            var content = await File.ReadAllTextAsync(csproj, cancellationToken);
            var originalContent = content;
            var projectName = System.IO.Path.GetFileNameWithoutExtension(csproj);

            foreach (var package in packagesToUpdate)
            {
                // Update Version attribute format
                var pattern = $@"(<PackageReference\s+Include=""{Regex.Escape(package)}""[^>]*Version="")[^""]+("")";
                content = Regex.Replace(content, pattern, $"${{1}}{targetVersion}${{2}}", RegexOptions.None, TimeSpan.FromSeconds(1));

                // Update <Version> element format
                pattern = $@"(<PackageReference\s+Include=""{Regex.Escape(package)}""[^>]*>\s*<Version>)[^<]+(</Version>)";
                content = Regex.Replace(content, pattern, $"${{1}}{targetVersion}${{2}}", RegexOptions.None, TimeSpan.FromSeconds(1));
            }

            if (content != originalContent)
            {
                await File.WriteAllTextAsync(csproj, content, cancellationToken);
                AnsiConsole.MarkupLine($"[green]✓[/] Updated [white]{projectName}[/]");
            }
        }

        // Restore packages
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[gray]Running dotnet restore...[/]");

        var restoreProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "restore",
                WorkingDirectory = folderPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        restoreProcess.Start();
        await restoreProcess.WaitForExitAsync(cancellationToken);

        if (restoreProcess.ExitCode != 0)
        {
            var error = await restoreProcess.StandardError.ReadToEndAsync(cancellationToken);
            AnsiConsole.MarkupLine($"[red]Restore failed: {Markup.Escape(error)}[/]");
            return false;
        }

        AnsiConsole.MarkupLine("[green]✓ Package restore complete[/]");
        return true;
    }

    private static async Task<bool> RunGenerateAsync(string solutionPath, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[gray]Running dotnet faes generate...[/]");

        var generateProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"faes generate \"{solutionPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        generateProcess.Start();

        // Stream output
        var outputTask = Task.Run(async () =>
        {
            while (!generateProcess.StandardOutput.EndOfStream)
            {
                var line = await generateProcess.StandardOutput.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    AnsiConsole.MarkupLine($"[gray]{Markup.Escape(line)}[/]");
                }
            }
        }, cancellationToken);

        await generateProcess.WaitForExitAsync(cancellationToken);
        await outputTask;

        if (generateProcess.ExitCode != 0)
        {
            return false;
        }

        AnsiConsole.MarkupLine("[green]✓ Code generation complete[/]");
        return true;
    }

    private static string? ResolveSolutionPath(Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Path))
        {
            settings.Path = FindSolutionFile();
            if (settings.Path == null)
            {
                AnsiConsole.MarkupLine("[red]No .sln or .slnx file was supplied and no file was found in the current directory.[/]");
                return null;
            }
        }

        return settings.Path;
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
