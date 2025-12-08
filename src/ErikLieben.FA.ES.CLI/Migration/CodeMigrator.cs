using System.Text.RegularExpressions;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.Migration;

/// <summary>
/// Handles code migrations between ErikLieben.FA.ES versions.
/// Applies breaking change migrations to source files.
/// </summary>
public class CodeMigrator
{
    private readonly string _folderPath;
    private readonly string _fromVersion;
    private readonly string _toVersion;
    private readonly bool _dryRun;

    public CodeMigrator(string folderPath, string fromVersion, string toVersion, bool dryRun)
    {
        _folderPath = folderPath;
        _fromVersion = fromVersion;
        _toVersion = toVersion;
        _dryRun = dryRun;
    }

    public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken)
    {
        var result = new MigrationResult { Success = true };

        // Determine which migrations to run based on version range
        var migrations = GetMigrationsForVersionRange(_fromVersion, _toVersion);

        if (migrations.Count == 0)
        {
            AnsiConsole.MarkupLine("[gray]No code migrations required for this version range[/]");
            return result;
        }

        AnsiConsole.MarkupLine($"[blue]Found {migrations.Count} migration(s) to apply[/]");
        AnsiConsole.WriteLine();

        // Get all .cs files (excluding obj/bin/generated)
        var csFiles = Directory.GetFiles(_folderPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ShouldIgnorePath(f))
            .ToList();

        AnsiConsole.MarkupLine($"[gray]Scanning {csFiles.Count} source files...[/]");

        foreach (var migration in migrations)
        {
            AnsiConsole.MarkupLine($"[blue]→[/] {migration.Description}");

            foreach (var file in csFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file, cancellationToken);
                    var newContent = migration.Apply(content);

                    if (content != newContent)
                    {
                        result.FilesModified++;
                        var relativePath = Path.GetRelativePath(_folderPath, file);

                        if (_dryRun)
                        {
                            AnsiConsole.MarkupLine($"  [yellow]Would modify:[/] {relativePath}");
                        }
                        else
                        {
                            await File.WriteAllTextAsync(file, newContent, cancellationToken);
                            AnsiConsole.MarkupLine($"  [green]✓[/] {relativePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"  [red]✗[/] Error processing {file}: {ex.Message}");
                    result.Success = false;
                }
            }
        }

        return result;
    }

    private static List<IMigration> GetMigrationsForVersionRange(string fromVersion, string toVersion)
    {
        var migrations = new List<IMigration>();

        // Parse versions
        if (!Version.TryParse(fromVersion.TrimStart('v'), out var from))
        {
            from = new Version(1, 0, 0);
        }

        if (!Version.TryParse(toVersion.TrimStart('v'), out var to))
        {
            to = new Version(2, 0, 0);
        }

        // v1.x to v2.0 migrations
        if (from.Major < 2 && to.Major >= 2)
        {
            migrations.Add(new RenameIEventUpcasterMigration());
            migrations.Add(new UpdateUpcasterNamingConventionMigration());
            migrations.Add(new StaticDocumentTagFactoryMigration());
            migrations.Add(new DeprecatedFoldOverloadMigration());
        }

        return migrations;
    }

    private static bool ShouldIgnorePath(string path)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        return normalizedPath.Contains("/obj/") ||
               normalizedPath.Contains("/bin/") ||
               normalizedPath.Contains("/.elfa/") ||
               normalizedPath.EndsWith(".generated.cs");
    }
}

/// <summary>
/// Result of a migration operation.
/// </summary>
public class MigrationResult
{
    public bool Success { get; set; }
    public int FilesModified { get; set; }
}

/// <summary>
/// Interface for code migrations.
/// </summary>
public interface IMigration
{
    string Description { get; }
    string Apply(string content);
}

/// <summary>
/// Migration: Rename IEventUpcaster to IUpcastEvent
/// </summary>
public class RenameIEventUpcasterMigration : IMigration
{
    public string Description => "Rename IEventUpcaster to IUpcastEvent";

    public string Apply(string content)
    {
        // Skip if file doesn't contain the old interface
        if (!content.Contains("IEventUpcaster"))
        {
            return content;
        }

        // Replace interface implementations
        content = Regex.Replace(
            content,
            @"\bIEventUpcaster\b",
            "IUpcastEvent",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // Update using statements if needed
        // The namespace hasn't changed, so no using updates needed

        return content;
    }
}

/// <summary>
/// Migration: Update *Upcaster class naming to *Upcast
/// </summary>
public class UpdateUpcasterNamingConventionMigration : IMigration
{
    public string Description => "Update *Upcaster class names to *Upcast naming convention";

    public string Apply(string content)
    {
        // Skip if file doesn't contain Upcaster classes
        if (!content.Contains("Upcaster"))
        {
            return content;
        }

        // Replace class names ending in "Upcaster" with "Upcast"
        // But only for event upcaster classes (be conservative)
        // Pattern: class SomeEventUpcaster -> class SomeEventUpcast
        content = Regex.Replace(
            content,
            @"\b(class\s+\w+Event)Upcaster\b",
            "$1Upcast",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // Also update references to these classes
        // Pattern: new SomeEventUpcaster -> new SomeEventUpcast
        content = Regex.Replace(
            content,
            @"\b(\w+Event)Upcaster\b(?!\s*:)",
            "$1Upcast",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        return content;
    }
}

/// <summary>
/// Migration: Replace static DocumentTagDocumentFactory.CreateDocumentTagStore() with instance method
/// </summary>
public class StaticDocumentTagFactoryMigration : IMigration
{
    public string Description => "Replace static DocumentTagDocumentFactory.CreateDocumentTagStore() with instance method";

    public string Apply(string content)
    {
        // Skip if file doesn't contain the static call
        if (!content.Contains("DocumentTagDocumentFactory.CreateDocumentTagStore"))
        {
            return content;
        }

        // Pattern: DocumentTagDocumentFactory.CreateDocumentTagStore() -> _documentTagFactory.CreateDocumentTagStore()
        // This is a best-effort migration - users may need to adjust the variable name
        content = Regex.Replace(
            content,
            @"DocumentTagDocumentFactory\.CreateDocumentTagStore\s*\(\s*\)",
            "_documentTagFactory.CreateDocumentTagStore()",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // Add a TODO comment if we made changes and the factory isn't already injected
        // Check for field declaration pattern (not just method call we just added)
        var hasFactoryField = Regex.IsMatch(content, @"(private|readonly|protected)\s+.*IDocumentTagDocumentFactory", RegexOptions.None, TimeSpan.FromSeconds(1));
        var hasFactoryFieldDeclaration = Regex.IsMatch(content, @"_documentTagFactory\s*[;=]", RegexOptions.None, TimeSpan.FromSeconds(1));

        if (!hasFactoryField && !hasFactoryFieldDeclaration)
        {
            // Add a comment at the top of the file to remind user to inject the factory
            var lines = content.Split('\n').ToList();
            var insertIndex = lines.FindIndex(l => l.TrimStart().StartsWith("namespace ") || l.TrimStart().StartsWith("public ") || l.TrimStart().StartsWith("internal "));
            if (insertIndex >= 0)
            {
                lines.Insert(insertIndex, "// TODO: Inject IDocumentTagDocumentFactory via constructor and store in _documentTagFactory field");
                lines.Insert(insertIndex, "");
                content = string.Join('\n', lines);
            }
        }

        return content;
    }
}

/// <summary>
/// Migration: Add obsolete warning comments for deprecated Fold overloads
/// </summary>
public class DeprecatedFoldOverloadMigration : IMigration
{
    public string Description => "Flag deprecated Fold(IObjectDocument) overloads for migration to VersionToken";

    public string Apply(string content)
    {
        // Skip if file doesn't contain Fold with IObjectDocument
        if (!content.Contains("IObjectDocument") || !content.Contains(".Fold"))
        {
            return content;
        }

        // Look for Fold method calls with IObjectDocument parameter
        // Pattern: .Fold(event, document) or Fold<T>(event, document, ...)
        var foldPattern = @"\.Fold\s*(<[^>]+>)?\s*\(\s*(@?\w+)\s*,\s*(\w+)\s*(?:,\s*[^)]+)?\)";

        // Check if the file contains Fold calls that might need migration
        var matches = Regex.Matches(content, foldPattern, RegexOptions.None, TimeSpan.FromSeconds(1));

        foreach (Match match in matches)
        {
            // Check if this looks like it's using IObjectDocument (variable named 'document' or similar)
            var secondParam = match.Groups[3].Value;
            if (secondParam.Contains("document", StringComparison.OrdinalIgnoreCase) ||
                secondParam.Contains("doc", StringComparison.OrdinalIgnoreCase))
            {
                // Add a TODO comment before this line
                var lineStart = content.LastIndexOf('\n', match.Index);
                if (lineStart >= 0)
                {
                    var indent = "";
                    var lineContent = content.Substring(lineStart + 1);
                    var indentMatch = Regex.Match(lineContent, @"^(\s*)", RegexOptions.None, TimeSpan.FromSeconds(1));
                    if (indentMatch.Success)
                    {
                        indent = indentMatch.Groups[1].Value;
                    }

                    // Only add TODO if not already present
                    var previousLine = content.Substring(Math.Max(0, lineStart - 100), Math.Min(100, lineStart));
                    if (!previousLine.Contains("TODO") && !previousLine.Contains("VersionToken"))
                    {
                        content = content.Insert(lineStart + 1, $"{indent}// TODO: Migrate to Fold(event, versionToken) - IObjectDocument overload is deprecated\n");
                    }
                }
            }
        }

        return content;
    }
}
