namespace ErikLieben.FA.ES.Testing.Performance;

/// <summary>
/// Contains performance metrics collected during measurement.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Gets or sets the total elapsed time for the operation.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Gets or sets the number of operations performed.
    /// </summary>
    public int OperationCount { get; set; }

    /// <summary>
    /// Gets or sets the total bytes allocated during the operation.
    /// </summary>
    public long AllocatedBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of Gen0 garbage collections.
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gets or sets the number of Gen1 garbage collections.
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gets or sets the number of Gen2 garbage collections.
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// Gets the total number of garbage collections across all generations.
    /// </summary>
    public int TotalCollections => Gen0Collections + Gen1Collections + Gen2Collections;

    /// <summary>
    /// Gets the operations per second.
    /// </summary>
    public double OperationsPerSecond =>
        ElapsedTime.TotalSeconds > 0 ? OperationCount / ElapsedTime.TotalSeconds : 0;

    /// <summary>
    /// Gets the average time per operation.
    /// </summary>
    public TimeSpan AverageTimePerOperation =>
        OperationCount > 0 ? TimeSpan.FromTicks(ElapsedTime.Ticks / OperationCount) : TimeSpan.Zero;

    /// <summary>
    /// Gets the average bytes allocated per operation.
    /// </summary>
    public double AllocatedBytesPerOperation =>
        OperationCount > 0 ? (double)AllocatedBytes / OperationCount : 0;

    /// <summary>
    /// Returns a string representation of the performance metrics.
    /// </summary>
    public override string ToString()
    {
        return $"Elapsed: {ElapsedTime.TotalMilliseconds:F2}ms, " +
               $"Ops: {OperationCount}, " +
               $"Ops/sec: {OperationsPerSecond:F2}, " +
               $"Allocated: {FormatBytes(AllocatedBytes)}, " +
               $"GC (Gen0/1/2): {Gen0Collections}/{Gen1Collections}/{Gen2Collections}";
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
