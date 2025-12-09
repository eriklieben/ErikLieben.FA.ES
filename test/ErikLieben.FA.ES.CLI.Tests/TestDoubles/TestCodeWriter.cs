using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.Abstractions;

namespace ErikLieben.FA.ES.CLI.Tests.TestDoubles;

/// <summary>
/// Test double for ICodeWriter that stores files in memory for test verification.
/// </summary>
public class TestCodeWriter : ICodeWriter
{
    private readonly ConcurrentDictionary<string, GeneratedFileResult> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly IActivityLogger? _logger;

    public TestCodeWriter(IActivityLogger? logger = null)
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
        _logger?.Log(ActivityType.FileGenerated, $"Generated (test): {Path.GetFileName(filePath)}");

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
    /// Check if a file path contains a pattern
    /// </summary>
    public bool HasFileMatching(string pattern) =>
        _files.Keys.Any(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Get content of a specific file
    /// </summary>
    public string? GetFileContent(string filePath) =>
        _files.TryGetValue(NormalizePath(filePath), out var result) ? result.GeneratedContent : null;

    /// <summary>
    /// Get file content by matching pattern
    /// </summary>
    public string? GetFileContentMatching(string pattern) =>
        _files.FirstOrDefault(kvp => kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .Value?.GeneratedContent;

    /// <summary>
    /// Get all file paths
    /// </summary>
    public IEnumerable<string> GetFilePaths() => _files.Keys;

    /// <summary>
    /// Assert a file exists and contains content
    /// </summary>
    public void AssertFileContains(string filePattern, string expectedContent)
    {
        var content = GetFileContentMatching(filePattern);
        if (content == null)
        {
            throw new InvalidOperationException($"No file matching pattern '{filePattern}' was written. Files written: {string.Join(", ", _files.Keys)}");
        }

        if (!content.Contains(expectedContent))
        {
            throw new InvalidOperationException($"File matching '{filePattern}' does not contain expected content '{expectedContent}'");
        }
    }

    /// <summary>
    /// Assert a file exists
    /// </summary>
    public void AssertFileExists(string filePattern)
    {
        if (!HasFileMatching(filePattern))
        {
            throw new InvalidOperationException($"No file matching pattern '{filePattern}' was written. Files written: {string.Join(", ", _files.Keys)}");
        }
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').ToLowerInvariant();
}
