namespace ErikLieben.FA.ES.EventStreamManagement.Progress;

using ErikLieben.FA.ES.EventStreamManagement.Core;

/// <summary>
/// Builder for configuring progress reporting.
/// </summary>
public class ProgressBuilder : IProgressBuilder
{
    private readonly ProgressConfiguration config = new();

    /// <inheritdoc/>
    public IProgressBuilder ReportEvery(TimeSpan interval)
    {
        config.ReportInterval = interval;
        return this;
    }

    /// <inheritdoc/>
    public IProgressBuilder OnProgress(Action<IMigrationProgress> callback)
    {
        config.OnProgress = callback;
        return this;
    }

    /// <inheritdoc/>
    public IProgressBuilder OnCompleted(Action<IMigrationProgress> callback)
    {
        config.OnCompleted = callback;
        return this;
    }

    /// <inheritdoc/>
    public IProgressBuilder OnFailed(Action<IMigrationProgress, Exception> callback)
    {
        config.OnFailed = callback;
        return this;
    }

    /// <inheritdoc/>
    public IProgressBuilder EnableLogging()
    {
        config.EnableLogging = true;
        return this;
    }

    /// <inheritdoc/>
    public IProgressBuilder TrackCustomMetric(string metricName, Func<object> collector)
    {
        config.CustomMetrics[metricName] = collector;
        return this;
    }

    /// <summary>
    /// Builds the progress configuration.
    /// </summary>
    internal ProgressConfiguration Build() => config;
}
