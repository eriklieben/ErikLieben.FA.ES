using System.Diagnostics;

namespace ErikLieben.FA.ES.Testing.Performance;

/// <summary>
/// Helper for measuring performance of operations.
/// </summary>
public static class PerformanceMeasurement
{
    /// <summary>
    /// Measures the performance of a synchronous operation.
    /// </summary>
    /// <param name="operation">The operation to measure.</param>
    /// <param name="operationCount">The number of operations performed (default is 1).</param>
    /// <param name="warmup">Whether to perform a warmup run before measurement (default is true).</param>
    /// <returns>The performance metrics.</returns>
    public static PerformanceMetrics Measure(Action operation, int operationCount = 1, bool warmup = true)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(operationCount), "Operation count must be greater than zero.");

        // Warmup run to ensure JIT compilation and caching
        if (warmup)
        {
            operation();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Record GC counts before
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Measure execution time
        var stopwatch = Stopwatch.StartNew();
        operation();
        stopwatch.Stop();

        // Record GC counts and memory after
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);

        return new PerformanceMetrics
        {
            ElapsedTime = stopwatch.Elapsed,
            OperationCount = operationCount,
            AllocatedBytes = Math.Max(0, memoryAfter - memoryBefore),
            Gen0Collections = gen0After - gen0Before,
            Gen1Collections = gen1After - gen1Before,
            Gen2Collections = gen2After - gen2Before
        };
    }

    /// <summary>
    /// Measures the performance of an asynchronous operation.
    /// </summary>
    /// <param name="operation">The async operation to measure.</param>
    /// <param name="operationCount">The number of operations performed (default is 1).</param>
    /// <param name="warmup">Whether to perform a warmup run before measurement (default is true).</param>
    /// <returns>The performance metrics.</returns>
    public static async Task<PerformanceMetrics> MeasureAsync(Func<Task> operation, int operationCount = 1, bool warmup = true)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(operationCount), "Operation count must be greater than zero.");

        // Warmup run
        if (warmup)
        {
            await operation();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Record GC counts before
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Measure execution time
        var stopwatch = Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();

        // Record GC counts and memory after
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);

        return new PerformanceMetrics
        {
            ElapsedTime = stopwatch.Elapsed,
            OperationCount = operationCount,
            AllocatedBytes = Math.Max(0, memoryAfter - memoryBefore),
            Gen0Collections = gen0After - gen0Before,
            Gen1Collections = gen1After - gen1Before,
            Gen2Collections = gen2After - gen2Before
        };
    }

    /// <summary>
    /// Measures the performance of repeated operations.
    /// </summary>
    /// <param name="operation">The operation to repeat and measure.</param>
    /// <param name="iterations">The number of times to repeat the operation.</param>
    /// <param name="warmup">Whether to perform a warmup run before measurement (default is true).</param>
    /// <returns>The performance metrics.</returns>
    public static PerformanceMetrics MeasureRepeated(Action operation, int iterations, bool warmup = true)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be greater than zero.");

        return Measure(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                operation();
            }
        }, iterations, warmup);
    }

    /// <summary>
    /// Measures the performance of repeated asynchronous operations.
    /// </summary>
    /// <param name="operation">The async operation to repeat and measure.</param>
    /// <param name="iterations">The number of times to repeat the operation.</param>
    /// <param name="warmup">Whether to perform a warmup run before measurement (default is true).</param>
    /// <returns>The performance metrics.</returns>
    public static async Task<PerformanceMetrics> MeasureRepeatedAsync(Func<Task> operation, int iterations, bool warmup = true)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be greater than zero.");

        return await MeasureAsync(async () =>
        {
            for (int i = 0; i < iterations; i++)
            {
                await operation();
            }
        }, iterations, warmup);
    }
}
