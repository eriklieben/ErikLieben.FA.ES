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
        var gitCheckFailed = await CheckGitPrerequisiteAsync(settings, folderPath, cancellationToken);
        if (gitCheckFailed)
        {
            return 1;
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
        if (IsAlreadyAtTargetVersion(currentVersion, targetVersion))
        {
            AnsiConsole.MarkupLine("[green]✓ Already at latest version[/]");
            return 0;
        }

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]DRY RUN - No changes will be made[/]");
            AnsiConsole.WriteLine();
        }

        // Step 5: Update NuGet packages
        var packageUpdateFailed = await UpdatePackageStepAsync(settings, folderPath, targetVersion, cancellationToken);
        if (packageUpdateFailed)
        {
            return 1;
        }

        // Step 6: Run code migrations
        var migrationFailed = await RunMigrationStepAsync(folderPath, currentVersion, targetVersion, settings.DryRun, cancellationToken);
        if (migrationFailed)
        {
            return 1;
        }

        // Step 7: Run generate
        var generateFailed = await RunGenerateStepAsync(settings, solutionPath, cancellationToken);
        if (generateFailed)
        {
            return 1;
        }

        // Summary
        PrintUpdateSummary(settings.DryRun, currentVersion, targetVersion);

        return 0;
    }

    private static async Task<bool> CheckGitPrerequisiteAsync(Settings settings, string folderPath, CancellationToken cancellationToken)
    {
        if (settings.SkipGitCheck)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Git check skipped - manual recovery may be needed on failure[/]");
            AnsiConsole.WriteLine();
            return false;
        }

        var gitCheckResult = await CheckGitStatusAsync(folderPath, cancellationToken);
        return !gitCheckResult.Success;
    }

    private static bool IsAlreadyAtTargetVersion(string currentVersion, string targetVersion)
    {
        return Version.TryParse(currentVersion.TrimStart('v'), out var current) &&
               Version.TryParse(targetVersion.TrimStart('v'), out var target) &&
               current >= target;
    }

    private static async Task<bool> UpdatePackageStepAsync(Settings settings, string folderPath, string targetVersion, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule("[blue]Updating Packages[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[gray]Would update ErikLieben.FA.ES.* packages to {0}[/]", targetVersion);
            return false;
        }

        var updateResult = await UpdatePackagesAsync(folderPath, targetVersion, cancellationToken);
        if (!updateResult)
        {
            AnsiConsole.MarkupLine("[red]✗ Package update failed[/]");
            AnsiConsole.MarkupLine("[yellow]You can restore the previous state with: git checkout .[/]");
            return true;
        }

        return false;
    }

    private static async Task<bool> RunMigrationStepAsync(string folderPath, string currentVersion, string targetVersion, bool dryRun, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[blue]Running Code Migrations[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        var migrator = new CodeMigrator(folderPath, currentVersion, targetVersion, dryRun);
        var migrationResult = await migrator.MigrateAsync(cancellationToken);

        if (!migrationResult.Success)
        {
            AnsiConsole.MarkupLine("[red]✗ Code migration failed[/]");
            AnsiConsole.MarkupLine("[yellow]You can restore the previous state with: git checkout .[/]");
            return true;
        }

        if (migrationResult.FilesModified > 0)
        {
            AnsiConsole.MarkupLine($"[green]✓ Modified {migrationResult.FilesModified} file(s)[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[gray]No code migrations needed[/]");
        }

        return false;
    }

    private static async Task<bool> RunGenerateStepAsync(Settings settings, string solutionPath, CancellationToken cancellationToken)
    {
        if (settings.SkipGenerate || settings.DryRun)
        {
            return false;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[blue]Regenerating Code[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        var generateResult = await RunGenerateAsync(solutionPath, cancellationToken);
        if (!generateResult)
        {
            AnsiConsole.MarkupLine("[red]✗ Code generation failed[/]");
            AnsiConsole.MarkupLine("[yellow]You can restore the previous state with: git checkout .[/]");
            return true;
        }

        return false;
    }

    private static void PrintUpdateSummary(bool dryRun, string currentVersion, string targetVersion)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Update Complete[/]").RuleStyle("green dim"));
        AnsiConsole.WriteLine();

        if (dryRun)
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
    }

    private static async Task<(bool Success, bool IsGitRepo, bool IsClean)> CheckGitStatusAsync(string folderPath, CancellationToken cancellationToken)
    {
        // Resolve git path from secure locations only
        var gitPath = ResolveGitPath();
        if (gitPath == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Git not found in secure system directories[/]");
            AnsiConsole.MarkupLine("[yellow]Make sure git is installed in a standard location (e.g., C:\\Program Files\\Git or /usr/bin/git).[/]");
            return (false, false, false);
        }

        // Check if it's a git repository
        var gitCheckProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = gitPath,
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
                FileName = gitPath,
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
        // Resolve dotnet path from secure locations only
        var dotnetPath = ResolveDotnetPath();
        if (dotnetPath == null)
        {
            // Fallback to hardcoded version if dotnet is not found in secure locations
            return "2.0.0";
        }

        // Query NuGet for latest version
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
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

        // Resolve dotnet path from secure locations only
        var dotnetPath = ResolveDotnetPath();
        if (dotnetPath == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Dotnet not found in secure system directories[/]");
            return false;
        }

        var restoreProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
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

        // Resolve dotnet path from secure locations only
        var dotnetPath = ResolveDotnetPath();
        if (dotnetPath == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Dotnet not found in secure system directories[/]");
            return false;
        }

        var generateProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
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
            string? line;
            while ((line = await generateProcess.StandardOutput.ReadLineAsync(cancellationToken)) != null)
            {
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

    /// <summary>
    /// Resolves the full path to the dotnet executable from secure system directories only.
    /// This prevents PATH manipulation attacks by only checking known safe locations.
    /// </summary>
    /// <returns>The full path to dotnet executable, or null if not found in secure locations.</returns>
    private static string? ResolveDotnetPath()
    {
        var knownPaths = OperatingSystem.IsWindows()
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                @"C:\Program Files\dotnet\dotnet.exe",
            }
            : new[]
            {
                "/usr/bin/dotnet",
                "/usr/local/bin/dotnet",
                "/usr/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
            };

        var secureDirectories = GetSecureDirectories(includeShareDotnet: true);
        return ResolveExecutableFromSecurePaths(knownPaths, secureDirectories, "dotnet");
    }

    /// <summary>
    /// Resolves the full path to the git executable from secure system directories only.
    /// This prevents PATH manipulation attacks by only checking known safe locations.
    /// </summary>
    /// <returns>The full path to git executable, or null if not found in secure locations.</returns>
    private static string? ResolveGitPath()
    {
        var knownPaths = OperatingSystem.IsWindows()
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe"),
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files (x86)\Git\cmd\git.exe",
            }
            : new[]
            {
                "/usr/bin/git",
                "/usr/local/bin/git",
                "/opt/homebrew/bin/git",
            };

        var secureDirectories = GetSecureDirectories(includeShareDotnet: false);
        return ResolveExecutableFromSecurePaths(knownPaths, secureDirectories, "git");
    }

    private static HashSet<string> GetSecureDirectories(bool includeShareDotnet)
    {
        if (OperatingSystem.IsWindows())
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"C:\Program Files",
                @"C:\Program Files (x86)",
                @"C:\Windows",
                @"C:\Windows\System32",
            };
        }

        var dirs = new HashSet<string>(StringComparer.Ordinal)
        {
            "/usr/bin",
            "/usr/local/bin",
            "/bin",
            "/opt/homebrew/bin",
        };

        if (includeShareDotnet)
        {
            dirs.Add("/usr/share/dotnet");
        }

        return dirs;
    }

    private static string? ResolveExecutableFromSecurePaths(string[] knownPaths, HashSet<string> secureDirectories, string executableName)
    {
        var found = knownPaths.FirstOrDefault(File.Exists);
        if (found != null)
        {
            return found;
        }

        return SearchPathForExecutable(secureDirectories, executableName);
    }

    private static string? SearchPathForExecutable(HashSet<string> secureDirectories, string executableName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var executable = OperatingSystem.IsWindows() ? $"{executableName}.exe" : executableName;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var isSecure = secureDirectories.Any(secure => dir.StartsWith(secure, comparison));
            if (!isSecure)
            {
                continue;
            }

            var executablePath = Path.Combine(dir, executable);
            if (File.Exists(executablePath))
            {
                return executablePath;
            }
        }

        return null;
    }
}
