namespace ErikLieben.FA.ES.CLI.Abstractions;

/// <summary>
/// Performance metrics collected during generation
/// </summary>
public record PerformanceMetrics(
    TimeSpan AnalysisDuration,
    TimeSpan GenerationDuration,
    TimeSpan FileWriteDuration,
    int FilesGenerated,
    int FilesSkipped,
    int ProjectsAnalyzed);

/// <summary>
/// Abstraction for tracking performance metrics during analysis and generation.
/// </summary>
public interface IPerformanceTracker
{
    /// <summary>
    /// Start tracking an operation
    /// </summary>
    /// <param name="operation">Name of the operation</param>
    /// <returns>Disposable that stops tracking when disposed</returns>
    IDisposable Track(string operation);

    /// <summary>
    /// Record a metric value
    /// </summary>
    void Record(string metric, int value);

    /// <summary>
    /// Increment a counter
    /// </summary>
    void Increment(string counter);

    /// <summary>
    /// Get collected metrics
    /// </summary>
    PerformanceMetrics GetMetrics();

    /// <summary>
    /// Reset all metrics
    /// </summary>
    void Reset();

    /// <summary>
    /// Track analysis operation
    /// </summary>
    IDisposable TrackAnalysis();

    /// <summary>
    /// Track generation operation
    /// </summary>
    IDisposable TrackGeneration();

    /// <summary>
    /// Track file write operation
    /// </summary>
    IDisposable TrackFileWrite();

    /// <summary>
    /// Record a file was generated
    /// </summary>
    void RecordFileGenerated();

    /// <summary>
    /// Record a file was skipped
    /// </summary>
    void RecordFileSkipped();

    /// <summary>
    /// Record a project was analyzed
    /// </summary>
    void RecordProjectAnalyzed();
}
