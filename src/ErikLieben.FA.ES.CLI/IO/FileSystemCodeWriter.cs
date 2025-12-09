using System.Collections.Concurrent;
using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.CodeGeneration;

namespace ErikLieben.FA.ES.CLI.IO;

/// <summary>
/// Code writer that writes generated files to the file system.
/// Includes code formatting, directory creation, and skip-if-unchanged optimization.
/// </summary>
public class FileSystemCodeWriter : ICodeWriter
{
    private readonly IActivityLogger _logger;
    private readonly ConcurrentBag<GeneratedFileResult> _writtenFiles = [];
    private readonly SemaphoreSlim _writeSemaphore;
    private readonly bool _skipUnchanged;

    /// <summary>
    /// Creates a new FileSystemCodeWriter
    /// </summary>
    /// <param name="logger">Activity logger for reporting progress</param>
    /// <param name="maxConcurrency">Maximum number of concurrent file writes (default: processor count)</param>
    /// <param name="skipUnchanged">Skip writing files that haven't changed (default: true)</param>
    public FileSystemCodeWriter(IActivityLogger logger, int? maxConcurrency = null, bool skipUnchanged = true)
    {
        _logger = logger;
        _writeSemaphore = new SemaphoreSlim(maxConcurrency ?? Environment.ProcessorCount);
        _skipUnchanged = skipUnchanged;
    }

    public async Task<GeneratedFileResult> WriteGeneratedFileAsync(
        string filePath,
        string content,
        string? projectDirectory = null,
        CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await WriteFileInternalAsync(filePath, content, projectDirectory, cancellationToken);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    private async Task<GeneratedFileResult> WriteFileInternalAsync(
        string filePath,
        string content,
        string? projectDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            // Format the code
            var formattedContent = CodeFormattingHelper.FormatCode(content, projectDirectory, cancellationToken);

            // Check if file exists and content is identical (optimization)
            if (_skipUnchanged && File.Exists(filePath))
            {
                var existingContent = await File.ReadAllTextAsync(filePath, cancellationToken);
                if (existingContent == formattedContent)
                {
                    var skippedResult = new GeneratedFileResult(filePath, true, null, formattedContent, Skipped: true);
                    _writtenFiles.Add(skippedResult);
                    _logger.Log(ActivityType.FileSkipped, $"Unchanged: {Path.GetFileName(filePath)}");
                    return skippedResult;
                }
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write the file
            await File.WriteAllTextAsync(filePath, formattedContent, cancellationToken);

            var result = new GeneratedFileResult(filePath, true, null, formattedContent, Skipped: false);
            _writtenFiles.Add(result);
            _logger.Log(ActivityType.FileGenerated, $"Generated: {Path.GetFileName(filePath)}");

            return result;
        }
        catch (Exception ex)
        {
            var errorResult = new GeneratedFileResult(filePath, false, ex.Message, content, Skipped: false);
            _writtenFiles.Add(errorResult);
            _logger.LogError($"Failed to write {Path.GetFileName(filePath)}", ex);
            return errorResult;
        }
    }

    public IReadOnlyList<GeneratedFileResult> GetWrittenFiles() =>
        _writtenFiles.ToList().AsReadOnly();

    public void Clear() => _writtenFiles.Clear();

    /// <summary>
    /// Get count of files actually written (excluding skipped)
    /// </summary>
    public int WrittenCount => _writtenFiles.Count(f => f.Success && !f.Skipped);

    /// <summary>
    /// Get count of files skipped (unchanged)
    /// </summary>
    public int SkippedCount => _writtenFiles.Count(f => f.Skipped);

    /// <summary>
    /// Get count of failed writes
    /// </summary>
    public int FailedCount => _writtenFiles.Count(f => !f.Success);
}
