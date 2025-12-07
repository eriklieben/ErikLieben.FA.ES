namespace ErikLieben.FA.ES.CLI.Abstractions;

/// <summary>
/// Result of a file generation operation
/// </summary>
public record GeneratedFileResult(
    string FilePath,
    bool Success,
    string? Error = null,
    string? GeneratedContent = null,
    bool Skipped = false);

/// <summary>
/// Abstraction for writing generated code files.
/// Allows different implementations for file system, in-memory (testing), etc.
/// </summary>
public interface ICodeWriter
{
    /// <summary>
    /// Write a generated file to the target location
    /// </summary>
    /// <param name="filePath">The full path where the file should be written</param>
    /// <param name="content">The content to write</param>
    /// <param name="projectDirectory">Optional project directory for relative path calculation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<GeneratedFileResult> WriteGeneratedFileAsync(
        string filePath,
        string content,
        string? projectDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all files written during this session (for testing/verification)
    /// </summary>
    IReadOnlyList<GeneratedFileResult> GetWrittenFiles();

    /// <summary>
    /// Clears the list of written files (useful for testing)
    /// </summary>
    void Clear();
}
