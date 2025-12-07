namespace ErikLieben.FA.ES.EventStreamManagement.Progress;

/// <summary>
/// Builder for configuring progress reporting options.
/// </summary>
public interface IProgressBuilder
{
    /// <summary>
    /// Sets how frequently to report progress updates.
    /// </summary>
    /// <param name="interval">The reporting interval.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IProgressBuilder ReportEvery(TimeSpan interval);

    /// <summary>
    /// Sets a callback to invoke on each progress update.
    /// </summary>
    /// <param name="callback">The progress callback.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IProgressBuilder OnProgress(Action<IMigrationProgress> callback);

    /// <summary>
    /// Sets a callback to invoke when migration completes.
    /// </summary>
    /// <param name="callback">The completion callback.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IProgressBuilder OnCompleted(Action<IMigrationProgress> callback);

    /// <summary>
    /// Sets a callback to invoke when migration fails.
    /// </summary>
    /// <param name="callback">The failure callback.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IProgressBuilder OnFailed(Action<IMigrationProgress, Exception> callback);

    /// <summary>
    /// Enables logging of progress to the configured logger.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IProgressBuilder EnableLogging();

    /// <summary>
    /// Sets custom metrics to track during migration.
    /// </summary>
    /// <param name="metricName">The metric name.</param>
    /// <param name="collector">Function to collect the metric value.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IProgressBuilder TrackCustomMetric(string metricName, Func<object> collector);
}
