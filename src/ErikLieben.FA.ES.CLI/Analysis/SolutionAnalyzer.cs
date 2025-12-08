using System.Diagnostics;
using System.Reflection;
using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Analyze;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Analysis;

/// <summary>
/// Analyzes solutions to extract aggregate, projection, and other definitions.
/// Uses the IActivityLogger abstraction for all output.
/// </summary>
public class SolutionAnalyzer : ISolutionAnalyzer
{
    private readonly IWorkspaceProvider _workspaceProvider;
    private readonly IActivityLogger _logger;
    private readonly Config _config;
    private readonly IPerformanceTracker? _performanceTracker;

    public SolutionAnalyzer(
        IWorkspaceProvider workspaceProvider,
        IActivityLogger logger,
        Config config,
        IPerformanceTracker? performanceTracker = null)
    {
        _workspaceProvider = workspaceProvider;
        _logger = logger;
        _config = config;
        _performanceTracker = performanceTracker;
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        var stopwatch = Stopwatch.StartNew();
        using var _ = _performanceTracker?.TrackAnalysis();

        _logger.Log(ActivityType.AnalysisStarted, "Starting solution analysis");
        _logger.Log(ActivityType.Info, $"Code generator version: {GetGeneratorVersion()}");
        _logger.Log(ActivityType.Info, $"Loading solution: {solutionPath}");

        var solution = await _workspaceProvider.OpenSolutionAsync(solutionPath, cancellationToken);

        var solutionDefinition = new SolutionDefinition
        {
            SolutionName = Path.GetFileNameWithoutExtension(solution.FilePath) ?? "Unknown",
            Generator = new GeneratorInformation
            {
                Version = GetGeneratorVersion()
            }
        };

        var solutionRootPath = Path.GetDirectoryName(solution.FilePath)
            ?? throw new InvalidOperationException("Failed to get solution root path");

        var maxConcurrency = Environment.ProcessorCount;
        var semaphore = new SemaphoreSlim(maxConcurrency);

        var totalProjects = solution.Projects.Count();
        var processedProjects = 0;

        var projectTasks = solution.Projects.Select(async project =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogProgress(processedProjects, totalProjects, $"Analyzing {project.Name}...");
                await ProcessProjectAsync(project, solutionDefinition, solutionRootPath, maxConcurrency, cancellationToken);
                Interlocked.Increment(ref processedProjects);
                _performanceTracker?.RecordProjectAnalyzed();
                _logger.LogProgress(processedProjects, totalProjects, $"Analyzed {project.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to analyze project: {project.Name}", ex);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(projectTasks);

        stopwatch.Stop();
        _logger.Log(ActivityType.AnalysisCompleted, $"Analysis complete ({stopwatch.Elapsed.TotalSeconds:F2}s)");

        return new AnalysisResult(solutionDefinition, solutionRootPath, stopwatch.Elapsed);
    }

    private async Task ProcessProjectAsync(
        Project project,
        SolutionDefinition solutionDefinition,
        string solutionRootPath,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        _logger.Log(ActivityType.Info, $"Loading project: {project.FilePath}");

        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
        {
            _logger.LogError($"Failed to compile project: {project.Name}");
            return;
        }

        if (_config.Es.EnableDiagnostics)
        {
            LogCompilationDiagnostics(compilation);
        }

        var projectDefinition = new ProjectDefinition
        {
            Name = project.Name,
            FileLocation = project.FilePath?.Replace(solutionRootPath, string.Empty) ?? string.Empty,
            Namespace = project.DefaultNamespace ?? string.Empty,
        };

        await ProcessSyntaxTreesAsync(compilation, projectDefinition, solutionRootPath, maxConcurrency, cancellationToken);

        if (HasRelevantDefinitions(projectDefinition))
        {
            lock (solutionDefinition)
            {
                solutionDefinition.Projects.Add(projectDefinition);
            }
        }
    }

    private static async Task ProcessSyntaxTreesAsync(
        Compilation compilation,
        ProjectDefinition projectDefinition,
        string solutionRootPath,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        var innerSemaphore = new SemaphoreSlim(maxConcurrency);
        var syntaxTreeTasks = compilation.SyntaxTrees.Select(async syntaxTree =>
        {
            await innerSemaphore.WaitAsync(cancellationToken);
            try
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(cancellationToken);

                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
                ProcessClassDeclarations(classDeclarations, semanticModel, compilation, projectDefinition, solutionRootPath);

                var recordDeclarations = root.DescendantNodes().OfType<RecordDeclarationSyntax>().ToList();
                ProcessRecordDeclarations(recordDeclarations, semanticModel, projectDefinition, solutionRootPath);
            }
            finally
            {
                innerSemaphore.Release();
            }
        });

        await Task.WhenAll(syntaxTreeTasks);
    }

    private static void ProcessClassDeclarations(
        List<ClassDeclarationSyntax> classDeclarations,
        SemanticModel semanticModel,
        Compilation compilation,
        ProjectDefinition projectDefinition,
        string solutionRootPath)
    {
        foreach (var classDeclaration in classDeclarations)
        {
            if (!TryGetUserDefinedClassSymbol(classDeclaration, semanticModel, out var classSymbol))
            {
                continue;
            }

            new AnalyzeAggregates(classSymbol, classDeclaration, semanticModel, compilation, solutionRootPath)
                .Run(projectDefinition.Aggregates);

            new AnalyzeInheritedAggregates(classSymbol, semanticModel, solutionRootPath)
                .Run(projectDefinition.InheritedAggregates);

            new AnalyzeProjections(classSymbol, semanticModel, compilation, solutionRootPath)
                .Run(projectDefinition.Projections);

            new AnalyzeVersionTokenOfTJsonConverter(classSymbol, solutionRootPath)
                .Run(projectDefinition.VersionTokenJsonConverterDefinitions);
        }
    }

    private static void ProcessRecordDeclarations(
        List<RecordDeclarationSyntax> recordDeclarations,
        SemanticModel semanticModel,
        ProjectDefinition projectDefinition,
        string solutionRootPath)
    {
        foreach (var recordDeclaration in recordDeclarations)
        {
            if (!TryGetUserDefinedRecordSymbol(recordDeclaration, semanticModel, out var recordSymbol))
            {
                continue;
            }

            new AnalyzeVersionTokenOfT(recordSymbol, solutionRootPath)
                .Run(projectDefinition.VersionTokens);
        }
    }

    private static bool TryGetUserDefinedClassSymbol(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        out INamedTypeSymbol classSymbol)
    {
        classSymbol = null!;

        if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol symbol)
        {
            return false;
        }

        if (symbol.ContainingAssembly.Name.StartsWith("ErikLieben.FA.ES"))
        {
            return false;
        }

        if (classDeclaration.SyntaxTree.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        classSymbol = symbol;
        return true;
    }

    private static bool TryGetUserDefinedRecordSymbol(
        RecordDeclarationSyntax recordDeclaration,
        SemanticModel semanticModel,
        out INamedTypeSymbol recordSymbol)
    {
        recordSymbol = null!;

        if (semanticModel.GetDeclaredSymbol(recordDeclaration) is not INamedTypeSymbol symbol)
        {
            return false;
        }

        if (symbol.ContainingAssembly.Name.StartsWith("ErikLieben.FA.ES"))
        {
            return false;
        }

        if (recordDeclaration.SyntaxTree.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        recordSymbol = symbol;
        return true;
    }

    private static bool HasRelevantDefinitions(ProjectDefinition projectDefinition)
    {
        return projectDefinition.Projections.Count != 0 ||
               projectDefinition.Aggregates.Count != 0 ||
               projectDefinition.InheritedAggregates.Count != 0 ||
               projectDefinition.VersionTokens.Count != 0;
    }

    private void LogCompilationDiagnostics(Compilation compilation)
    {
        var diagnostics = compilation.GetDiagnostics();
        foreach (var diagnostic in diagnostics)
        {
            var message = $"{diagnostic.Severity} {diagnostic.Id} - {diagnostic.GetMessage()}";

            switch (diagnostic.Severity)
            {
                case DiagnosticSeverity.Error:
                    _logger.LogError(message);
                    break;
                case DiagnosticSeverity.Warning:
                    _logger.Log(ActivityType.Warning, message);
                    break;
                default:
                    _logger.Log(ActivityType.Info, message);
                    break;
            }
        }
    }

    private static string? GetGeneratorVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version;
    }
}
