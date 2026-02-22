using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Abstractions;

/// <summary>
/// Result of solution analysis
/// </summary>
public record AnalysisResult(
    SolutionDefinition Solution,
    string SolutionRootPath,
    TimeSpan Duration);

/// <summary>
/// Abstraction for analyzing solutions to extract aggregate, projection, and other definitions.
/// </summary>
public interface ISolutionAnalyzer
{
    /// <summary>
    /// Analyze a solution and extract all relevant definitions
    /// </summary>
    /// <param name="solutionPath">Path to the solution file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis result containing solution definition and metadata</returns>
    Task<AnalysisResult> AnalyzeAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);
}
