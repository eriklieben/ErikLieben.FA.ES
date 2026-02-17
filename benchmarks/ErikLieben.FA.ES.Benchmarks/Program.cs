using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Running;
using ErikLieben.FA.ES.Benchmarks;

// Run all benchmarks or specific ones based on command line args
// Usage:
//   dotnet run -c Release                           # Interactive menu to select benchmarks
//   dotnet run -c Release -- --filter *Json*        # Run only JSON-related benchmarks
//   dotnet run -c Release -- --list flat            # List all available benchmarks
//   dotnet run -c Release -- --all                  # Run all benchmarks
//   dotnet run -c Release -- --config quick         # Quick run for development
//   dotnet run -c Release -- --config ci            # CI-optimized run
//   dotnet run -c Release -- --config baseline      # Baseline comparison mode

#if DEBUG
Console.WriteLine("WARNING: Running benchmarks in DEBUG mode will produce unreliable results.");
Console.WriteLine("Please run with: dotnet run -c Release");
Console.WriteLine();
#endif

var runAll = args.Contains("--all");
var configArg = GetArgumentValue(args, "--config");
var filteredArgs = args
    .Where(a => a != "--all")
    .Where((a, i) => a != "--config" && (i == 0 || args[i - 1] != "--config"))
    .ToArray();

// If --all with no filter specified, run all benchmarks
if (runAll && !filteredArgs.Any(a => a.StartsWith("--filter")))
{
    filteredArgs = ["--filter", "*"];
}

// Select configuration based on --config argument
IConfig config = configArg?.ToLowerInvariant() switch
{
    "fast" => new FastBenchmarkConfig(),
    "quick" => new QuickBenchmarkConfig(),
    "ci" => new CiBenchmarkConfig(),
    "baseline" => new BaselineComparisonConfig(),
    "default" => new DefaultBenchmarkConfig(),
    "multi" or "multiruntime" => new MultiRuntimeConfig(),
    "quickmulti" => new QuickMultiRuntimeConfig(),
    _ => DefaultConfig.Instance
        .AddExporter(HtmlExporter.Default)
        .AddExporter(CatppuccinHtmlExporter.Default)
        .AddExporter(CsvExporter.Default)
        .AddExporter(MarkdownExporter.GitHub)
};

// If no args provided, show a summary of available benchmarks
if (filteredArgs.Length == 0)
{
    Console.WriteLine("ErikLieben.FA.ES Benchmarks");
    Console.WriteLine("===========================");
    Console.WriteLine();
    Console.WriteLine("Available benchmark suites:");
    Console.WriteLine();
    Console.WriteLine("  Core:");
    Console.WriteLine("    - EventStreamBenchmarks        - Event stream read/append/stream operations");
    Console.WriteLine("    - SessionBenchmarks            - Session commit and multi-event operations");
    Console.WriteLine("    - ChunkingBenchmarks           - Chunked vs non-chunked read/append comparison");
    Console.WriteLine();
    Console.WriteLine("  Serialization:");
    Console.WriteLine("    - JsonEventBenchmarks          - Event serialization/deserialization");
    Console.WriteLine();
    Console.WriteLine("  Folding:");
    Console.WriteLine("    - AggregateFoldBenchmarks      - Aggregate folding hot path");
    Console.WriteLine();
    Console.WriteLine("  Registry:");
    Console.WriteLine("    - EventTypeRegistryBenchmarks  - Registry lookups (frozen vs mutable)");
    Console.WriteLine();
    Console.WriteLine("  Upcasting:");
    Console.WriteLine("    - EventUpcasterBenchmarks      - Schema version upcasting chains");
    Console.WriteLine();
    Console.WriteLine("  Parsing:");
    Console.WriteLine("    - VersionTokenBenchmarks       - Token parsing and creation");
    Console.WriteLine();
    Console.WriteLine("  Storage:");
    Console.WriteLine("    - InMemoryDataStoreBenchmarks  - In-memory storage read/write baseline");
    Console.WriteLine();
    Console.WriteLine("  Concurrency:");
    Console.WriteLine("    - ConcurrentSessionBenchmarks  - Parallel vs sequential writer throughput");
    Console.WriteLine();
    Console.WriteLine("  Snapshots:");
    Console.WriteLine("    - SnapshotLoadBenchmarks       - With vs without snapshot loading comparison");
    Console.WriteLine("    - SnapshotOperationBenchmarks  - Snapshot save/load/list operations");
    Console.WriteLine("    - SnapshotBreakEvenBenchmarks  - Find break-even point for snapshotting");
    Console.WriteLine();
    Console.WriteLine("Run options:");
    Console.WriteLine("  dotnet run -c Release -- --all                  # Run all benchmarks");
    Console.WriteLine("  dotnet run -c Release -- --filter *Json*        # Run JSON benchmarks only");
    Console.WriteLine("  dotnet run -c Release -- --filter *Registry*    # Run registry benchmarks only");
    Console.WriteLine("  dotnet run -c Release -- --filter *Session*     # Run session benchmarks only");
    Console.WriteLine("  dotnet run -c Release -- --filter *Snapshot*    # Run snapshot benchmarks only");
    Console.WriteLine("  dotnet run -c Release -- --filter *Chunking*    # Run chunking benchmarks only");
    Console.WriteLine("  dotnet run -c Release -- --filter *Concurrent*  # Run concurrency benchmarks only");
    Console.WriteLine("  dotnet run -c Release -- --list flat            # List all benchmarks");
    Console.WriteLine();
    Console.WriteLine("Configurations (--config <name>):");
    Console.WriteLine("  default     - Standard BenchmarkDotNet defaults with HTML/CSV/MD exporters");
    Console.WriteLine("  quick       - ShortRun job for fast development feedback");
    Console.WriteLine("  ci          - MediumRun job optimized for CI pipelines");
    Console.WriteLine("  baseline    - Full run with baseline comparison columns");
    Console.WriteLine("  multi       - ShortRun with all exporters (for runtime comparison)");
    Console.WriteLine("  quickmulti  - Dry run for quick feedback");
    Console.WriteLine();
    Console.WriteLine("Multi-runtime comparison (.NET 9 vs .NET 10):");
    Console.WriteLine("  dotnet run -c Release -f net9.0 -- --filter *Json* --config multi");
    Console.WriteLine("  dotnet run -c Release -f net10.0 -- --filter *Json* --config multi");
    Console.WriteLine();
    Console.WriteLine("Reports will be saved to: BenchmarkDotNet.Artifacts/results/");
    Console.WriteLine("  - HTML reports  (*.html)");
    Console.WriteLine("  - CSV exports   (*.csv)");
    Console.WriteLine("  - Markdown      (*-github.md)");
    Console.WriteLine("  - JSON          (*-full.json) [baseline/ci modes]");
    Console.WriteLine();
}

// Run the benchmark switcher which allows selecting which benchmarks to run
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(filteredArgs, config);

static string? GetArgumentValue(string[] args, string argName)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == argName)
            return args[i + 1];
    }
    return null;
}
