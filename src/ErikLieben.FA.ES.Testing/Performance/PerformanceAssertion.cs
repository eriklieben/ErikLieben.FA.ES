namespace ErikLieben.FA.ES.Testing.Performance;

/// <summary>
/// Provides fluent assertions for performance metrics.
/// </summary>
public class PerformanceAssertion
{
    private readonly PerformanceMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceAssertion"/> class.
    /// </summary>
    /// <param name="metrics">The performance metrics to assert against.</param>
    public PerformanceAssertion(PerformanceMetrics metrics)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <summary>
    /// Gets the performance metrics.
    /// </summary>
    public PerformanceMetrics Metrics => _metrics;

    /// <summary>
    /// Asserts that the elapsed time is less than the specified duration.
    /// </summary>
    /// <param name="maxDuration">The maximum allowed duration.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public PerformanceAssertion ShouldCompleteWithin(TimeSpan maxDuration)
    {
        if (_metrics.ElapsedTime > maxDuration)
        {
            throw new TestAssertionException(
                $"Operation took {_metrics.ElapsedTime.TotalMilliseconds:F2}ms but should complete within {maxDuration.TotalMilliseconds:F2}ms.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the throughput is at least the specified operations per second.
    /// </summary>
    /// <param name="minOpsPerSecond">The minimum required operations per second.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public PerformanceAssertion ShouldHaveThroughputOf(double minOpsPerSecond)
    {
        if (_metrics.OperationsPerSecond < minOpsPerSecond)
        {
            throw new TestAssertionException(
                $"Throughput was {_metrics.OperationsPerSecond:F2} ops/sec but should be at least {minOpsPerSecond:F2} ops/sec.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the allocated memory is less than the specified amount in bytes.
    /// </summary>
    /// <param name="maxBytes">The maximum allowed allocated bytes.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public PerformanceAssertion ShouldAllocateLessThan(long maxBytes)
    {
        if (_metrics.AllocatedBytes > maxBytes)
        {
            throw new TestAssertionException(
                $"Allocated {FormatBytes(_metrics.AllocatedBytes)} but should allocate less than {FormatBytes(maxBytes)}.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the allocated memory per operation is less than the specified amount in bytes.
    /// </summary>
    /// <param name="maxBytesPerOp">The maximum allowed allocated bytes per operation.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public PerformanceAssertion ShouldAllocateLessThanPerOperation(double maxBytesPerOp)
    {
        if (_metrics.AllocatedBytesPerOperation > maxBytesPerOp)
        {
            throw new TestAssertionException(
                $"Allocated {FormatBytes((long)_metrics.AllocatedBytesPerOperation)} per operation but should allocate less than {FormatBytes((long)maxBytesPerOp)} per operation.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that no garbage collections occurred during the operation.
    /// </summary>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public PerformanceAssertion ShouldNotCauseGarbageCollection()
    {
        if (_metrics.TotalCollections > 0)
        {
            throw new TestAssertionException(
                $"Operation caused {_metrics.TotalCollections} garbage collection(s) " +
                $"(Gen0: {_metrics.Gen0Collections}, Gen1: {_metrics.Gen1Collections}, Gen2: {_metrics.Gen2Collections}) " +
                $"but should not cause any.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that no Gen2 garbage collections occurred during the operation.
    /// </summary>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public PerformanceAssertion ShouldNotCauseGen2Collection()
    {
        if (_metrics.Gen2Collections > 0)
        {
            throw new TestAssertionException(
                $"Operation caused {_metrics.Gen2Collections} Gen2 garbage collection(s) but should not cause any.");
        }

        return this;
    }

    /// <summary>
    /// Asserts that the average time per operation is less than the specified duration.
    /// </summary>
    /// <param name="maxAverageDuration">The maximum allowed average duration per operation.</param>
    /// <returns>This assertion for chaining.</returns>
    /// <exception cref="TestAssertionException">Thrown when the assertion fails.</exception>
    public PerformanceAssertion ShouldHaveAverageTimePerOperationOf(TimeSpan maxAverageDuration)
    {
        if (_metrics.AverageTimePerOperation > maxAverageDuration)
        {
            throw new TestAssertionException(
                $"Average time per operation was {_metrics.AverageTimePerOperation.TotalMilliseconds:F2}ms " +
                $"but should be less than {maxAverageDuration.TotalMilliseconds:F2}ms.");
        }

        return this;
    }

    /// <summary>
    /// Executes a custom assertion against the metrics.
    /// </summary>
    /// <param name="assertion">The custom assertion to execute.</param>
    /// <returns>This assertion for chaining.</returns>
    public PerformanceAssertion Should(Action<PerformanceMetrics> assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        assertion(_metrics);
        return this;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:F2} {sizes[order]}";
    }
}
