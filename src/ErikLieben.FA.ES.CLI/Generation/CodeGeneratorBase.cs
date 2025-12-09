using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Generation;

/// <summary>
/// Base class for code generators providing common functionality.
/// </summary>
public abstract class CodeGeneratorBase : ICodeGenerator
{
    protected readonly IActivityLogger Logger;
    protected readonly ICodeWriter CodeWriter;
    protected readonly Config Config;

    /// <summary>
    /// Name of this generator (for logging)
    /// </summary>
    public abstract string Name { get; }

    protected CodeGeneratorBase(
        IActivityLogger logger,
        ICodeWriter codeWriter,
        Config config)
    {
        Logger = logger;
        CodeWriter = codeWriter;
        Config = config;
    }

    /// <summary>
    /// Generate code for the solution
    /// </summary>
    public abstract Task GenerateAsync(
        SolutionDefinition solution,
        string solutionPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Helper to write code to a file with logging
    /// </summary>
    protected async Task<GeneratedFileResult> WriteCodeAsync(
        string filePath,
        string code,
        string? projectDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = await CodeWriter.WriteGeneratedFileAsync(filePath, code, projectDirectory, cancellationToken);

        if (!result.Success)
        {
            Logger.LogError($"Failed to generate: {filePath}", null);
        }

        return result;
    }

    /// <summary>
    /// Gets the generated file path for a source file
    /// </summary>
    protected static string GetGeneratedFilePath(string solutionPath, string sourceFilePath)
    {
        var rel = (sourceFilePath ?? string.Empty).Replace('\\', '/');
        var relGen = rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            ? string.Concat(rel.AsSpan(0, rel.Length - 3), ".Generated.cs")
            : rel + ".Generated.cs";
        var normalized = relGen.Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(solutionPath, normalized);
    }

    /// <summary>
    /// Gets the project directory from a file path
    /// </summary>
    protected static string? GetProjectDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            if (Directory.GetFiles(directory, "*.csproj").Length > 0)
            {
                return directory;
            }
            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }
}
