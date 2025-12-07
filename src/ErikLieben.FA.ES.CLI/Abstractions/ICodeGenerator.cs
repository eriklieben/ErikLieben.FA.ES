using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Abstractions;

/// <summary>
/// Abstraction for code generators that produce source files from solution definitions.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Name of this generator (for logging and identification)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Generate code based on the solution definition
    /// </summary>
    /// <param name="solution">The analyzed solution definition</param>
    /// <param name="solutionPath">Root path of the solution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task GenerateAsync(
        SolutionDefinition solution,
        string solutionPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Extended interface for generators that support incremental generation.
/// Only regenerates files affected by changes.
/// </summary>
public interface IIncrementalGenerator : ICodeGenerator
{
    /// <summary>
    /// Returns true if this generator should run based on changed files
    /// </summary>
    /// <param name="changedFiles">List of files that changed</param>
    /// <param name="solution">Current solution definition</param>
    /// <returns>True if regeneration is needed</returns>
    bool ShouldRegenerate(IReadOnlyList<string> changedFiles, SolutionDefinition solution);

    /// <summary>
    /// Generate only for affected entities based on changed files
    /// </summary>
    /// <param name="solution">The analyzed solution definition</param>
    /// <param name="changedFiles">List of files that changed</param>
    /// <param name="solutionPath">Root path of the solution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task GenerateIncrementalAsync(
        SolutionDefinition solution,
        IReadOnlyList<string> changedFiles,
        string solutionPath,
        CancellationToken cancellationToken = default);
}
