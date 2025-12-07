namespace ErikLieben.FA.ES.EventStreamManagement.Progress;

using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

/// <summary>
/// Tracks and reports migration progress.
/// </summary>
public class MigrationProgressTracker
{
    private readonly Guid migrationId;
    private readonly ProgressConfiguration? config;
    private readonly ILogger logger;
    private readonly Stopwatch stopwatch;
    private readonly Dictionary<string, object> customMetrics;

    private long eventsProcessed;
    private long totalEvents;
    private MigrationStatus status;
    private MigrationPhase currentPhase;
    private bool isPaused;
    private string? errorMessage;

    private DateTimeOffset lastReportTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationProgressTracker"/> class.
    /// </summary>
    public MigrationProgressTracker(
        Guid migrationId,
        ProgressConfiguration? config,
        ILogger logger)
    {
        this.migrationId = migrationId;
        this.config = config;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.stopwatch = Stopwatch.StartNew();
        this.customMetrics = new Dictionary<string, object>();
        this.status = MigrationStatus.Pending;
        this.currentPhase = MigrationPhase.Normal;
        this.lastReportTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets or sets the total number of events to process.
    /// </summary>
    public long TotalEvents
    {
        get => totalEvents;
        set => totalEvents = value;
    }

    /// <summary>
    /// Increments the processed event count.
    /// </summary>
    public void IncrementProcessed(long count = 1)
    {
        Interlocked.Add(ref eventsProcessed, count);
        CheckAndReport();
    }

    /// <summary>
    /// Sets the current migration status.
    /// </summary>
    public void SetStatus(MigrationStatus newStatus)
    {
        status = newStatus;
        Report();
    }

    /// <summary>
    /// Sets the current migration phase.
    /// </summary>
    public void SetPhase(MigrationPhase phase)
    {
        currentPhase = phase;
        Report();
    }

    /// <summary>
    /// Sets the paused state.
    /// </summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        if (paused)
        {
            stopwatch.Stop();
        }
        else
        {
            stopwatch.Start();
        }
        Report();
    }

    /// <summary>
    /// Sets an error message.
    /// </summary>
    public void SetError(string error)
    {
        errorMessage = error;
        status = MigrationStatus.Failed;
        Report();
    }

    /// <summary>
    /// Adds or updates a custom metric.
    /// </summary>
    public void SetCustomMetric(string name, object value)
    {
        lock (customMetrics)
        {
            customMetrics[name] = value;
        }
    }

    /// <summary>
    /// Gets the current progress snapshot.
    /// </summary>
    public IMigrationProgress GetProgress()
    {
        var elapsed = stopwatch.Elapsed;
        var processed = Interlocked.Read(ref eventsProcessed);
        var total = Interlocked.Read(ref totalEvents);

        var percentageComplete = total > 0 ? (double)processed / total * 100.0 : 0.0;
        var eventsPerSecond = elapsed.TotalSeconds > 0 ? processed / elapsed.TotalSeconds : 0.0;

        TimeSpan? estimatedRemaining = null;
        if (eventsPerSecond > 0 && total > processed)
        {
            var remaining = total - processed;
            estimatedRemaining = TimeSpan.FromSeconds(remaining / eventsPerSecond);
        }

        Dictionary<string, object> metrics;
        lock (customMetrics)
        {
            metrics = new Dictionary<string, object>(customMetrics);
        }

        // Collect custom metrics if configured
        if (config?.CustomMetrics != null)
        {
            foreach (var (name, collector) in config.CustomMetrics)
            {
                try
                {
                    metrics[name] = collector();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error collecting custom metric {MetricName}", name);
                }
            }
        }

        return new MigrationProgress
        {
            MigrationId = migrationId,
            Status = status,
            CurrentPhase = currentPhase,
            PercentageComplete = percentageComplete,
            EventsProcessed = processed,
            TotalEvents = total,
            EventsPerSecond = eventsPerSecond,
            Elapsed = elapsed,
            EstimatedRemaining = estimatedRemaining,
            IsPaused = isPaused,
            CanPause = true,
            CanRollback = true,
            CustomMetrics = metrics,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Forces an immediate progress report.
    /// </summary>
    public void Report()
    {
        var progress = GetProgress();

        // Call configured callback
        config?.OnProgress?.Invoke(progress);

        // Log if enabled
        if (config?.EnableLogging == true)
        {
            logger.LogInformation(
                "Migration {MigrationId} progress: {Percentage:F1}% ({Processed}/{Total} events, {Rate:F0} events/sec)",
                migrationId,
                progress.PercentageComplete,
                progress.EventsProcessed,
                progress.TotalEvents,
                progress.EventsPerSecond);
        }

        lastReportTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reports completion.
    /// </summary>
    public void ReportCompleted()
    {
        stopwatch.Stop();
        status = MigrationStatus.Completed;
        var progress = GetProgress();

        config?.OnCompleted?.Invoke(progress);

        if (config?.EnableLogging == true)
        {
            logger.LogInformation(
                "Migration {MigrationId} completed in {Elapsed} ({EventCount} events at {Rate:F0} events/sec)",
                migrationId,
                progress.Elapsed,
                progress.EventsProcessed,
                progress.EventsPerSecond);
        }
    }

    /// <summary>
    /// Reports failure.
    /// </summary>
    public void ReportFailed(Exception exception)
    {
        stopwatch.Stop();
        status = MigrationStatus.Failed;
        errorMessage = exception.Message;
        var progress = GetProgress();

        config?.OnFailed?.Invoke(progress, exception);

        if (config?.EnableLogging == true)
        {
            logger.LogError(
                exception,
                "Migration {MigrationId} failed after {Elapsed} ({EventCount} events processed)",
                migrationId,
                progress.Elapsed,
                progress.EventsProcessed);
        }
    }

    private void CheckAndReport()
    {
        if (config == null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - lastReportTime >= config.ReportInterval)
        {
            Report();
        }
    }
}
