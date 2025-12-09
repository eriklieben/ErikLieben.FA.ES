using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Abstractions;

/// <summary>
/// Result of a full generation run
/// </summary>
public record GenerationResult(
    SolutionDefinition Solution,
    IReadOnlyList<GeneratedFileResult> GeneratedFiles,
    TimeSpan AnalysisDuration,
    TimeSpan GenerationDuration,
    int FilesSkipped);

/// <summary>
/// Orchestrates the full analysis and code generation pipeline.
/// </summary>
public interface IGenerationOrchestrator
{
    /// <summary>
    /// Run full analysis and generation for a solution
    /// </summary>
    /// <param name="solutionPath">Path to the solution file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing generated files and timing information</returns>
    Task<GenerationResult> GenerateAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run incremental generation based on changed files
    /// </summary>
    /// <param name="solutionPath">Path to the solution file</param>
    /// <param name="changedFiles">List of files that changed</param>
    /// <param name="previousSolution">Previous solution state (for diffing)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing generated files and timing information</returns>
    Task<GenerationResult> GenerateIncrementalAsync(
        string solutionPath,
        IReadOnlyList<string> changedFiles,
        SolutionDefinition? previousSolution = null,
        CancellationToken cancellationToken = default);
}
