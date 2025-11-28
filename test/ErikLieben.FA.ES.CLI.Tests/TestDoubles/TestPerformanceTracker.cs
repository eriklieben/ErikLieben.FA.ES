using System;
using System.Collections.Generic;
using ErikLieben.FA.ES.CLI.Abstractions;

namespace ErikLieben.FA.ES.CLI.Tests.TestDoubles;

/// <summary>
/// Test double for IPerformanceTracker that records metrics for verification.
/// </summary>
public class TestPerformanceTracker : IPerformanceTracker
{
    private readonly Dictionary<string, TimeSpan> _durations = new();
    private readonly Dictionary<string, int> _counters = new();

    public IDisposable Track(string operation)
    {
        return new TrackingScope(this, operation);
    }

    public void Record(string metric, int value)
    {
        _counters[metric] = value;
    }

    public void Increment(string counter)
    {
        if (!_counters.TryGetValue(counter, out var value))
        {
            value = 0;
        }
        _counters[counter] = value + 1;
    }

    public PerformanceMetrics GetMetrics()
    {
        return new PerformanceMetrics(
            AnalysisDuration: _durations.GetValueOrDefault("analysis", TimeSpan.Zero),
            GenerationDuration: _durations.GetValueOrDefault("generation", TimeSpan.Zero),
            FileWriteDuration: _durations.GetValueOrDefault("fileWrite", TimeSpan.Zero),
            FilesGenerated: _counters.GetValueOrDefault("filesGenerated", 0),
            FilesSkipped: _counters.GetValueOrDefault("filesSkipped", 0),
            ProjectsAnalyzed: _counters.GetValueOrDefault("projectsAnalyzed", 0)
        );
    }

    public void Reset()
    {
        _durations.Clear();
        _counters.Clear();
    }

    public IDisposable TrackAnalysis() => Track("analysis");
    public IDisposable TrackGeneration() => Track("generation");
    public IDisposable TrackFileWrite() => Track("fileWrite");
    public void RecordFileGenerated() => Increment("filesGenerated");
    public void RecordFileSkipped() => Increment("filesSkipped");
    public void RecordProjectAnalyzed() => Increment("projectsAnalyzed");

    /// <summary>
    /// Get duration for a specific operation
    /// </summary>
    public TimeSpan GetDuration(string operation) =>
        _durations.GetValueOrDefault(operation, TimeSpan.Zero);

    /// <summary>
    /// Get counter value
    /// </summary>
    public int GetCounter(string counter) =>
        _counters.GetValueOrDefault(counter, 0);

    internal void SetDuration(string operation, TimeSpan duration)
    {
        _durations[operation] = duration;
    }

    private class TrackingScope : IDisposable
    {
        private readonly TestPerformanceTracker _tracker;
        private readonly string _operation;
        private readonly DateTime _start;

        public TrackingScope(TestPerformanceTracker tracker, string operation)
        {
            _tracker = tracker;
            _operation = operation;
            _start = DateTime.UtcNow;
        }

        public void Dispose()
        {
            _tracker.SetDuration(_operation, DateTime.UtcNow - _start);
        }
    }
}
