using System.Collections.Concurrent;
using System.Diagnostics;
using ErikLieben.FA.ES.CLI.Abstractions;

namespace ErikLieben.FA.ES.CLI.IO;

/// <summary>
/// Tracks performance metrics during analysis and generation.
/// </summary>
public class PerformanceTracker : IPerformanceTracker
{
    private readonly ConcurrentDictionary<string, Stopwatch> _timers = new();
    private readonly ConcurrentDictionary<string, long> _durations = new();
    private readonly ConcurrentDictionary<string, int> _counters = new();

    private const string AnalysisOperation = "analysis";
    private const string GenerationOperation = "generation";
    private const string FileWriteOperation = "fileWrite";
    private const string FilesGeneratedCounter = "filesGenerated";
    private const string FilesSkippedCounter = "filesSkipped";
    private const string ProjectsAnalyzedCounter = "projectsAnalyzed";

    public IDisposable Track(string operation)
    {
        var sw = Stopwatch.StartNew();
        _timers[operation] = sw;
        return new TrackingScope(this, operation, sw);
    }

    public void Record(string metric, int value)
    {
        _counters[metric] = value;
    }

    public void Increment(string counter)
    {
        _counters.AddOrUpdate(counter, 1, (_, v) => v + 1);
    }

    public PerformanceMetrics GetMetrics()
    {
        return new PerformanceMetrics(
            AnalysisDuration: GetDuration(AnalysisOperation),
            GenerationDuration: GetDuration(GenerationOperation),
            FileWriteDuration: GetDuration(FileWriteOperation),
            FilesGenerated: GetCounter(FilesGeneratedCounter),
            FilesSkipped: GetCounter(FilesSkippedCounter),
            ProjectsAnalyzed: GetCounter(ProjectsAnalyzedCounter)
        );
    }

    public void Reset()
    {
        _timers.Clear();
        _durations.Clear();
        _counters.Clear();
    }

    private TimeSpan GetDuration(string operation)
    {
        if (_durations.TryGetValue(operation, out var ms))
        {
            return TimeSpan.FromMilliseconds(ms);
        }

        if (_timers.TryGetValue(operation, out var sw))
        {
            return sw.Elapsed;
        }

        return TimeSpan.Zero;
    }

    private int GetCounter(string counter)
    {
        return _counters.TryGetValue(counter, out var value) ? value : 0;
    }

    internal void CompleteOperation(string operation, long elapsedMs)
    {
        _durations[operation] = elapsedMs;
        _timers.TryRemove(operation, out _);
    }

    /// <summary>
    /// Track analysis operation
    /// </summary>
    public IDisposable TrackAnalysis() => Track(AnalysisOperation);

    /// <summary>
    /// Track generation operation
    /// </summary>
    public IDisposable TrackGeneration() => Track(GenerationOperation);

    /// <summary>
    /// Track file write operation
    /// </summary>
    public IDisposable TrackFileWrite() => Track(FileWriteOperation);

    /// <summary>
    /// Record a file was generated
    /// </summary>
    public void RecordFileGenerated() => Increment(FilesGeneratedCounter);

    /// <summary>
    /// Record a file was skipped
    /// </summary>
    public void RecordFileSkipped() => Increment(FilesSkippedCounter);

    /// <summary>
    /// Record a project was analyzed
    /// </summary>
    public void RecordProjectAnalyzed() => Increment(ProjectsAnalyzedCounter);

    private sealed class TrackingScope : IDisposable
    {
        private readonly PerformanceTracker _tracker;
        private readonly string _operation;
        private readonly Stopwatch _stopwatch;

        public TrackingScope(PerformanceTracker tracker, string operation, Stopwatch stopwatch)
        {
            _tracker = tracker;
            _operation = operation;
            _stopwatch = stopwatch;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _tracker.CompleteOperation(_operation, _stopwatch.ElapsedMilliseconds);
        }
    }
}
