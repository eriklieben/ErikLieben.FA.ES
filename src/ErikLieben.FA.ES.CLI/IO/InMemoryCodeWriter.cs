using System.Collections.Concurrent;
using ErikLieben.FA.ES.CLI.Abstractions;

namespace ErikLieben.FA.ES.CLI.IO;

/// <summary>
/// Code writer that stores files in memory instead of the file system.
/// Useful for testing without side effects.
/// </summary>
public class InMemoryCodeWriter : ICodeWriter
{
    private readonly ConcurrentDictionary<string, GeneratedFileResult> _files = new();
    private readonly IActivityLogger? _logger;

    public InMemoryCodeWriter(IActivityLogger? logger = null)
    {
        _logger = logger;
    }

    public Task<GeneratedFileResult> WriteGeneratedFileAsync(
        string filePath,
        string content,
        string? projectDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizePath(filePath);
        var result = new GeneratedFileResult(normalizedPath, true, null, content, Skipped: false);

        _files[normalizedPath] = result;
        _logger?.Log(ActivityType.FileGenerated, $"Generated (in-memory): {Path.GetFileName(filePath)}");

        return Task.FromResult(result);
    }

    public IReadOnlyList<GeneratedFileResult> GetWrittenFiles() =>
        _files.Values.ToList().AsReadOnly();

    public void Clear() => _files.Clear();

    /// <summary>
    /// Check if a file was written
    /// </summary>
    public bool HasFile(string filePath) =>
        _files.ContainsKey(NormalizePath(filePath));

    /// <summary>
    /// Get content of a specific file
    /// </summary>
    public string? GetFileContent(string filePath) =>
        _files.TryGetValue(NormalizePath(filePath), out var result) ? result.GeneratedContent : null;

    /// <summary>
    /// Get all file paths
    /// </summary>
    public IEnumerable<string> GetFilePaths() => _files.Keys;

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').ToLowerInvariant();
}
