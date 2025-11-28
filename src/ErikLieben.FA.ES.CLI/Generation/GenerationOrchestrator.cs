using System.Diagnostics;
using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Generation;

/// <summary>
/// Orchestrates the full analysis and code generation pipeline.
/// Coordinates analyzers and generators with parallel execution support.
/// </summary>
public class GenerationOrchestrator : IGenerationOrchestrator
{
    private readonly ISolutionAnalyzer _analyzer;
    private readonly IEnumerable<ICodeGenerator> _generators;
    private readonly IActivityLogger _logger;
    private readonly ICodeWriter _codeWriter;
    private readonly IPerformanceTracker? _performanceTracker;
    private readonly bool _parallelGeneration;

    public GenerationOrchestrator(
        ISolutionAnalyzer analyzer,
        IEnumerable<ICodeGenerator> generators,
        IActivityLogger logger,
        ICodeWriter codeWriter,
        IPerformanceTracker? performanceTracker = null,
        bool parallelGeneration = true)
    {
        _analyzer = analyzer;
        _generators = generators;
        _logger = logger;
        _codeWriter = codeWriter;
        _performanceTracker = performanceTracker;
        _parallelGeneration = parallelGeneration;
    }

    public async Task<GenerationResult> GenerateAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        _logger.Log(ActivityType.GenerationStarted, "Starting code generation");

        // Phase 1: Analysis
        var analysisResult = await _analyzer.AnalyzeAsync(solutionPath, cancellationToken);

        // Phase 2: Generation
        var generationStopwatch = Stopwatch.StartNew();
        using var _ = _performanceTracker?.TrackGeneration();

        if (_parallelGeneration)
        {
            await RunGeneratorsInParallelAsync(analysisResult.Solution, analysisResult.SolutionRootPath, cancellationToken);
        }
        else
        {
            await RunGeneratorsSequentiallyAsync(analysisResult.Solution, analysisResult.SolutionRootPath, cancellationToken);
        }

        generationStopwatch.Stop();

        var writtenFiles = _codeWriter.GetWrittenFiles();
        var skippedCount = writtenFiles.Count(f => f.Skipped);

        totalStopwatch.Stop();
        _logger.Log(ActivityType.GenerationCompleted, $"Generation complete ({totalStopwatch.Elapsed.TotalSeconds:F2}s)");

        return new GenerationResult(
            analysisResult.Solution,
            writtenFiles,
            analysisResult.Duration,
            generationStopwatch.Elapsed,
            skippedCount);
    }

    public async Task<GenerationResult> GenerateIncrementalAsync(
        string solutionPath,
        IReadOnlyList<string> changedFiles,
        SolutionDefinition? previousSolution = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        _logger.Log(ActivityType.GenerationStarted, $"Starting incremental generation ({changedFiles.Count} changed files)");

        // Phase 1: Analysis (always needed for fresh state)
        var analysisResult = await _analyzer.AnalyzeAsync(solutionPath, cancellationToken);

        // Phase 2: Incremental Generation
        var generationStopwatch = Stopwatch.StartNew();
        using var _ = _performanceTracker?.TrackGeneration();

        var incrementalGenerators = _generators.OfType<IIncrementalGenerator>().ToList();
        var regularGenerators = _generators.Except(incrementalGenerators.Cast<ICodeGenerator>()).ToList();

        // Run incremental generators only if they need to regenerate
        foreach (var generator in incrementalGenerators)
        {
            if (generator.ShouldRegenerate(changedFiles, analysisResult.Solution))
            {
                _logger.Log(ActivityType.Info, $"Running incremental {generator.Name}");
                await generator.GenerateIncrementalAsync(
                    analysisResult.Solution,
                    changedFiles,
                    analysisResult.SolutionRootPath,
                    cancellationToken);
            }
            else
            {
                _logger.Log(ActivityType.Info, $"Skipping {generator.Name} (no changes)");
            }
        }

        // Regular generators run normally (they don't support incremental)
        if (_parallelGeneration && regularGenerators.Count > 0)
        {
            await Task.WhenAll(regularGenerators.Select(g =>
                g.GenerateAsync(analysisResult.Solution, analysisResult.SolutionRootPath, cancellationToken)));
        }
        else
        {
            foreach (var generator in regularGenerators)
            {
                await generator.GenerateAsync(analysisResult.Solution, analysisResult.SolutionRootPath, cancellationToken);
            }
        }

        generationStopwatch.Stop();

        var writtenFiles = _codeWriter.GetWrittenFiles();
        var skippedCount = writtenFiles.Count(f => f.Skipped);

        totalStopwatch.Stop();
        _logger.Log(ActivityType.GenerationCompleted, $"Incremental generation complete ({totalStopwatch.Elapsed.TotalSeconds:F2}s)");

        return new GenerationResult(
            analysisResult.Solution,
            writtenFiles,
            analysisResult.Duration,
            generationStopwatch.Elapsed,
            skippedCount);
    }

    private async Task RunGeneratorsInParallelAsync(
        SolutionDefinition solution,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        var tasks = _generators.Select(async generator =>
        {
            try
            {
                await generator.GenerateAsync(solution, solutionPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Generator {generator.Name} failed", ex);
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task RunGeneratorsSequentiallyAsync(
        SolutionDefinition solution,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        foreach (var generator in _generators)
        {
            try
            {
                await generator.GenerateAsync(solution, solutionPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Generator {generator.Name} failed", ex);
            }
        }
    }

    /// <summary>
    /// Factory method to create an orchestrator with all standard generators.
    /// </summary>
    public static GenerationOrchestrator CreateDefault(
        IActivityLogger logger,
        ICodeWriter codeWriter,
        Config config,
        IWorkspaceProvider? workspaceProvider = null,
        IPerformanceTracker? performanceTracker = null,
        bool parallelGeneration = true)
    {
        workspaceProvider ??= new IO.MsBuildWorkspaceProvider();

        var analyzer = new Analysis.SolutionAnalyzer(workspaceProvider, logger, config, performanceTracker);

        var generators = new List<ICodeGenerator>
        {
            new AggregateCodeGenerator(logger, codeWriter, config),
            new ProjectionCodeGenerator(logger, codeWriter, config),
            new InheritedAggregateCodeGenerator(logger, codeWriter, config),
            new ExtensionCodeGenerator(logger, codeWriter, config),
            new VersionTokenCodeGenerator(logger, codeWriter, config),
            new VersionTokenJsonConverterCodeGenerator(logger, codeWriter, config)
        };

        return new GenerationOrchestrator(
            analyzer,
            generators,
            logger,
            codeWriter,
            performanceTracker,
            parallelGeneration);
    }
}
