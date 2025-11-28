using ErikLieben.FA.ES.CLI.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace ErikLieben.FA.ES.CLI.IO;

/// <summary>
/// Workspace provider that uses MSBuild to load solutions.
/// Used in production for loading actual solution files.
/// </summary>
public class MsBuildWorkspaceProvider : IWorkspaceProvider
{
    public async Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        var workspace = MSBuildWorkspace.Create();
        return await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
    }
}
