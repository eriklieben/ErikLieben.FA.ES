using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Running;

// Run all benchmarks or specific ones based on command line args
// Usage:
//   dotnet run -c Release                           # Interactive menu to select benchmarks
//   dotnet run -c Release -- --filter *Json*        # Run only JSON-related benchmarks
//   dotnet run -c Release -- --list flat            # List all available benchmarks
//   dotnet run -c Release -- --all                  # Run all benchmarks

#if DEBUG
Console.WriteLine("WARNING: Running benchmarks in DEBUG mode will produce unreliable results.");
Console.WriteLine("Please run with: dotnet run -c Release");
Console.WriteLine();
#endif

var runAll = args.Contains("--all");
var filteredArgs = args.Where(a => a != "--all").ToArray();

// If --all with no filter specified, run all benchmarks
if (runAll && !filteredArgs.Any(a => a.StartsWith("--filter")))
{
    filteredArgs = ["--filter", "*"];
}

// Configure with exporters for HTML, CSV, and Markdown
var config = DefaultConfig.Instance
    .AddExporter(HtmlExporter.Default)
    .AddExporter(CsvExporter.Default)
    .AddExporter(MarkdownExporter.GitHub);

// If no args provided, show a summary of available benchmarks
if (filteredArgs.Length == 0)
{
    Console.WriteLine("ErikLieben.FA.ES Benchmarks");
    Console.WriteLine("===========================");
    Console.WriteLine();
    Console.WriteLine("Available benchmark suites:");
    Console.WriteLine("  1. JsonEventBenchmarks         - Event serialization/deserialization");
    Console.WriteLine("  2. EventTypeRegistryBenchmarks - Registry lookups (frozen vs mutable)");
    Console.WriteLine("  3. EventUpcasterBenchmarks     - Schema version upcasting chains");
    Console.WriteLine("  4. InMemoryDataStoreBenchmarks - In-memory storage read/write");
    Console.WriteLine("  5. VersionTokenBenchmarks      - Token parsing and creation");
    Console.WriteLine();
    Console.WriteLine("Run options:");
    Console.WriteLine("  dotnet run -c Release -- --all                  # Run all benchmarks");
    Console.WriteLine("  dotnet run -c Release -- --filter *Json*        # Run JSON benchmarks only");
    Console.WriteLine("  dotnet run -c Release -- --filter *Registry*    # Run registry benchmarks only");
    Console.WriteLine("  dotnet run -c Release -- --list flat            # List all benchmarks");
    Console.WriteLine();
    Console.WriteLine("Reports will be saved to: BenchmarkDotNet.Artifacts/results/");
    Console.WriteLine("  - HTML reports  (*.html)");
    Console.WriteLine("  - CSV exports   (*.csv)");
    Console.WriteLine("  - Markdown      (*-github.md)");
    Console.WriteLine();
}

// Run the benchmark switcher which allows selecting which benchmarks to run
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(filteredArgs, config);
