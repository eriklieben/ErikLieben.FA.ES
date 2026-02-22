using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Reports;

namespace ErikLieben.FA.ES.Benchmarks;

/// <summary>
/// Caches benchmark results to enable combining cold-start and warm results in a single report.
/// </summary>
public static class BenchmarkResultsCache
{
    private const string CacheFileName = "benchmark-results-cache.json";

    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetCachePath(string resultsDirectory)
    {
        return Path.Combine(resultsDirectory, CacheFileName);
    }

    /// <summary>
    /// Saves benchmark results to the cache file.
    /// </summary>
    public static void SaveResults(Summary summary)
    {
        var cachePath = GetCachePath(summary.ResultsDirectoryPath);
        var cache = LoadCache(cachePath);

        var isColdStart = summary.Reports.Any(r =>
            r.BenchmarkCase.Job.Run.RunStrategy == BenchmarkDotNet.Engines.RunStrategy.ColdStart);

        var runType = isColdStart ? "cold-start" : "warm";

        foreach (var report in summary.Reports)
        {
            if (report.ResultStatistics == null) continue;

            var methodName = report.BenchmarkCase.Descriptor.WorkloadMethod.Name;
            var key = $"{methodName}_{runType}";

            var result = new CachedBenchmarkResult
            {
                MethodName = methodName,
                RunType = runType,
                MeanNanoseconds = report.ResultStatistics.Mean,
                AllocatedBytes = report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase) ?? 0,
                Timestamp = DateTime.UtcNow
            };

            cache.Results[key] = result;
        }

        SaveCache(cachePath, cache);
    }

    /// <summary>
    /// Loads cached results for generating the comparison table.
    /// </summary>
    public static BenchmarkCache LoadCache(string cachePath)
    {
        if (!File.Exists(cachePath))
        {
            return new BenchmarkCache();
        }

        try
        {
            var json = File.ReadAllText(cachePath);
            return JsonSerializer.Deserialize<BenchmarkCache>(json, CachedJsonOptions) ?? new BenchmarkCache();
        }
        catch
        {
            return new BenchmarkCache();
        }
    }

    private static void SaveCache(string cachePath, BenchmarkCache cache)
    {
        var json = JsonSerializer.Serialize(cache, CachedJsonOptions);
        File.WriteAllText(cachePath, json);
    }

    /// <summary>
    /// Gets a comparison table data from the cache.
    /// Returns null if data for both cold-start and warm runs is not available.
    /// </summary>
    public static ComparisonTableData? GetComparisonData(string resultsDirectory)
    {
        var cachePath = GetCachePath(resultsDirectory);
        var cache = LoadCache(cachePath);

        if (cache.Results.Count == 0)
            return null;

        var data = new ComparisonTableData();

        // Group by base method name (without _SourceGen/_Reflection suffix)
        var operations = new[] { "Serialize_Small", "Serialize_Medium", "Serialize_Large", "Deserialize_Small", "Deserialize_Medium", "Deserialize_Large" };

        foreach (var op in operations)
        {
            var row = new ComparisonRow { Operation = FormatOperationName(op) };

            // Cold-start Source-Gen
            if (cache.Results.TryGetValue($"{op}_SourceGen_cold-start", out var coldSourceGen))
            {
                row.ColdStartSourceGen = FormatTime(coldSourceGen.MeanNanoseconds);
                row.ColdStartSourceGenBytes = coldSourceGen.AllocatedBytes;
            }

            // Cold-start Reflection
            if (cache.Results.TryGetValue($"{op}_Reflection_cold-start", out var coldReflection))
            {
                row.ColdStartReflection = FormatTime(coldReflection.MeanNanoseconds);
                row.ColdStartReflectionBytes = coldReflection.AllocatedBytes;
            }

            // Warm Source-Gen
            if (cache.Results.TryGetValue($"{op}_SourceGen_warm", out var warmSourceGen))
            {
                row.WarmSourceGen = FormatTime(warmSourceGen.MeanNanoseconds);
                row.WarmSourceGenBytes = warmSourceGen.AllocatedBytes;
            }

            // Warm Reflection
            if (cache.Results.TryGetValue($"{op}_Reflection_warm", out var warmReflection))
            {
                row.WarmReflection = FormatTime(warmReflection.MeanNanoseconds);
                row.WarmReflectionBytes = warmReflection.AllocatedBytes;
            }

            // Only add row if we have at least some data
            if (row.HasAnyData)
            {
                data.Rows.Add(row);
            }
        }

        data.LastUpdated = cache.Results.Values.Max(r => r.Timestamp);
        data.HasColdStartData = cache.Results.Keys.Any(k => k.Contains("cold-start"));
        data.HasWarmData = cache.Results.Keys.Any(k => k.Contains("warm"));

        return data.Rows.Count > 0 ? data : null;
    }

    private static string FormatOperationName(string op)
    {
        return op.Replace("_", " (") + ")";
    }

    private static string FormatTime(double nanoseconds)
    {
        if (nanoseconds >= 1_000_000_000) // >= 1 second
            return $"{nanoseconds / 1_000_000_000:F2} s";
        if (nanoseconds >= 1_000_000) // >= 1 millisecond
            return $"{nanoseconds / 1_000_000:F2} ms";
        if (nanoseconds >= 1_000) // >= 1 microsecond
            return $"{nanoseconds / 1_000:F2} us";
        return $"{nanoseconds:F2} ns";
    }
}

public class BenchmarkCache
{
    [JsonPropertyName("results")]
    public Dictionary<string, CachedBenchmarkResult> Results { get; set; } = new();
}

public class CachedBenchmarkResult
{
    [JsonPropertyName("methodName")]
    public string MethodName { get; set; } = "";

    [JsonPropertyName("runType")]
    public string RunType { get; set; } = "";

    [JsonPropertyName("meanNanoseconds")]
    public double MeanNanoseconds { get; set; }

    [JsonPropertyName("allocatedBytes")]
    public long AllocatedBytes { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

public class ComparisonTableData
{
    public List<ComparisonRow> Rows { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public bool HasColdStartData { get; set; }
    public bool HasWarmData { get; set; }
}

public class ComparisonRow
{
    public string Operation { get; set; } = "";
    public string? ColdStartSourceGen { get; set; }
    public string? ColdStartReflection { get; set; }
    public string? WarmSourceGen { get; set; }
    public string? WarmReflection { get; set; }
    public long ColdStartSourceGenBytes { get; set; }
    public long ColdStartReflectionBytes { get; set; }
    public long WarmSourceGenBytes { get; set; }
    public long WarmReflectionBytes { get; set; }

    public bool HasAnyData =>
        ColdStartSourceGen != null || ColdStartReflection != null ||
        WarmSourceGen != null || WarmReflection != null;
}
