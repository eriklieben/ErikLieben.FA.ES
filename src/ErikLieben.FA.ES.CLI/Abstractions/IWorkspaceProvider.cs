using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Abstractions;

/// <summary>
/// Abstraction for providing Roslyn workspace/solution access.
/// Allows different implementations for MSBuild workspace (production) and AdhocWorkspace (testing).
/// </summary>
public interface IWorkspaceProvider
{
    /// <summary>
    /// Opens a solution from the specified path
    /// </summary>
    /// <param name="solutionPath">Path to the solution file (.sln or .slnx)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The loaded solution</returns>
    Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default);
}
