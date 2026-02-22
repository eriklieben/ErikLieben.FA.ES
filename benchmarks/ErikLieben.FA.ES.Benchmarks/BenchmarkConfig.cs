using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Validators;

namespace ErikLieben.FA.ES.Benchmarks;

/// <summary>
/// Default configuration for all benchmarks.
/// Includes HTML, CSV, Markdown, and JSON exporters plus memory diagnostics.
/// </summary>
public class DefaultBenchmarkConfig : ManualConfig
{
    public DefaultBenchmarkConfig()
    {
        // Add console logger for progress output
        AddLogger(ConsoleLogger.Default);

        // Add default columns (Method, Job, Categories, Params, etc.)
        AddColumnProvider(DefaultColumnProviders.Instance);

        // Use default job with memory diagnostics
        AddDiagnoser(MemoryDiagnoser.Default);

        // Add exporters for various report formats
        AddExporter(HtmlExporter.Default);
        AddExporter(CatppuccinHtmlExporter.Default);
        AddExporter(CsvExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);

        // Add additional statistics columns
        AddColumn(StatisticColumn.Median);
        AddColumn(StatisticColumn.P95);
        AddColumn(RankColumn.Arabic);

        // Order benchmarks by mean time
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        // Enable all validations
        AddValidator(JitOptimizationsValidator.FailOnError);
        AddValidator(ReturnValueValidator.FailOnError);

        // Summary style
        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend);
    }
}

/// <summary>
/// Quick benchmark configuration for development feedback.
/// Uses ShortRun job for faster iteration.
/// </summary>
public class QuickBenchmarkConfig : ManualConfig
{
    public QuickBenchmarkConfig()
    {
        // Add console logger for progress output
        AddLogger(ConsoleLogger.Default);

        // Add default columns (Method, Job, Categories, Params, etc.)
        AddColumnProvider(DefaultColumnProviders.Instance);

        // Use ShortRun job for quick feedback
        AddJob(Job.ShortRun);
        AddDiagnoser(MemoryDiagnoser.Default);

        // Exporters
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);
        AddExporter(CatppuccinHtmlExporter.Default);

        // Add rank column
        AddColumn(RankColumn.Arabic);

        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
    }
}

/// <summary>
/// Fast benchmark configuration designed to complete all benchmarks in under 30 minutes.
/// Uses minimal iterations while still providing meaningful comparative data.
/// </summary>
public class FastBenchmarkConfig : ManualConfig
{
    public FastBenchmarkConfig()
    {
        // Add console logger for progress output
        AddLogger(ConsoleLogger.Default);

        // Add default columns (Method, Job, Categories, Params, etc.)
        AddColumnProvider(DefaultColumnProviders.Instance);

        // Custom very short job: 1 launch, 3 warmup, 5 iterations
        // This gives enough samples for comparison while being fast
        var fastJob = Job.Default
            .WithLaunchCount(1)
            .WithWarmupCount(3)
            .WithIterationCount(5)
            .WithId("Fast");

        AddJob(fastJob);
        AddDiagnoser(MemoryDiagnoser.Default);

        // Full set of exporters including JSON for the web UI
        AddExporter(HtmlExporter.Default);
        AddExporter(CatppuccinHtmlExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);

        // Add rank column
        AddColumn(RankColumn.Arabic);

        // Order by fastest
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend);
    }
}

/// <summary>
/// Baseline comparison configuration.
/// Compares current results against a baseline run.
/// </summary>
public class BaselineComparisonConfig : ManualConfig
{
    public BaselineComparisonConfig()
    {
        // Add console logger for progress output
        AddLogger(ConsoleLogger.Default);

        // Add default columns (Method, Job, Categories, Params, etc.)
        AddColumnProvider(DefaultColumnProviders.Instance);

        AddDiagnoser(MemoryDiagnoser.Default);

        // Full set of exporters including JSON for baseline storage
        AddExporter(HtmlExporter.Default);
        AddExporter(CatppuccinHtmlExporter.Default);
        AddExporter(CsvExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);

        // Ratio columns for comparison
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumn(BaselineRatioColumn.RatioStdDev);
        AddColumn(RankColumn.Arabic);

        // Configure for baseline comparison style output
        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend);

        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        AddValidator(JitOptimizationsValidator.FailOnError);
    }
}

/// <summary>
/// CI-optimized configuration.
/// Balanced between accuracy and execution time for CI pipelines.
/// </summary>
public class CiBenchmarkConfig : ManualConfig
{
    public CiBenchmarkConfig()
    {
        // Add console logger for progress output
        AddLogger(ConsoleLogger.Default);

        // Add default columns (Method, Job, Categories, Params, etc.)
        AddColumnProvider(DefaultColumnProviders.Instance);

        // Medium job for CI - faster than default but still reliable
        AddJob(Job.MediumRun);
        AddDiagnoser(MemoryDiagnoser.Default);

        AddExporter(HtmlExporter.Default);
        AddExporter(CatppuccinHtmlExporter.Default);
        AddExporter(CsvExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);

        AddColumn(RankColumn.Arabic);

        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend);

        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        AddValidator(JitOptimizationsValidator.FailOnError);
    }
}

/// <summary>
/// Multi-runtime configuration for comparing .NET 9 vs .NET 10 performance.
/// Runs benchmarks on both runtimes for side-by-side comparison.
/// </summary>
/// <remarks>
/// Note: BenchmarkDotNet 0.14.0 doesn't have built-in .NET 10 support yet.
/// The project is multi-targeted (net9.0;net10.0), so BenchmarkDotNet will
/// automatically run on whatever runtime the benchmark exe is built for.
/// To compare runtimes, build and run separately for each TFM:
///   dotnet run -c Release -f net9.0 -- --filter *Json*
///   dotnet run -c Release -f net10.0 -- --filter *Json*
/// </remarks>
public class MultiRuntimeConfig : ManualConfig
{
    public MultiRuntimeConfig()
    {
        // Add console logger for progress output
        AddLogger(ConsoleLogger.Default);

        // Add default columns (Method, Job, Categories, Params, etc.)
        AddColumnProvider(DefaultColumnProviders.Instance);

        // Run with ShortRun job on current runtime
        // User should run once with -f net9.0 and once with -f net10.0 to compare
        AddJob(Job.ShortRun);

        AddDiagnoser(MemoryDiagnoser.Default);

        // Exporters
        AddExporter(HtmlExporter.Default);
        AddExporter(CatppuccinHtmlExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);

        // Add rank column
        AddColumn(RankColumn.Arabic);

        // Group by runtime for easy comparison
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend);
    }
}

/// <summary>
/// Quick multi-runtime configuration for fast .NET 9 vs .NET 10 comparison.
/// Uses fewer iterations for faster feedback during development.
/// </summary>
public class QuickMultiRuntimeConfig : ManualConfig
{
    public QuickMultiRuntimeConfig()
    {
        // Add console logger for progress output
        AddLogger(ConsoleLogger.Default);

        // Add default columns (Method, Job, Categories, Params, etc.)
        AddColumnProvider(DefaultColumnProviders.Instance);

        // Dry run job for quick feedback
        AddJob(Job.Dry);

        AddDiagnoser(MemoryDiagnoser.Default);

        // Exporters including JSON for runtime comparison
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CatppuccinHtmlExporter.Default);
        AddExporter(JsonExporter.Full);

        // Add rank column
        AddColumn(RankColumn.Arabic);

        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
    }
}
