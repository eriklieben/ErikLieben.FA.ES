using System.Threading;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.Abstractions;
using Microsoft.CodeAnalysis;

namespace ErikLieben.FA.ES.CLI.Tests.TestDoubles;

/// <summary>
/// Test double for IWorkspaceProvider that returns a pre-configured AdhocWorkspace solution.
/// </summary>
public class TestWorkspaceProvider : IWorkspaceProvider
{
    private readonly Solution _solution;

    public TestWorkspaceProvider(Solution solution)
    {
        _solution = solution;
    }

    public TestWorkspaceProvider(AdhocWorkspace workspace)
    {
        _solution = workspace.CurrentSolution;
    }

    public Task<Solution> OpenSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_solution);
    }
}
