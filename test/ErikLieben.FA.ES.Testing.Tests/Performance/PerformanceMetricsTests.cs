using ErikLieben.FA.ES.Testing.Performance;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Performance;

public class PerformanceMetricsTests
{
    [Fact]
    public void TotalCollections_should_return_sum_of_all_generations()
    {
        var metrics = new PerformanceMetrics
        {
            Gen0Collections = 5,
            Gen1Collections = 3,
            Gen2Collections = 1
        };

        Assert.Equal(9, metrics.TotalCollections);
    }

    [Fact]
    public void OperationsPerSecond_should_calculate_correctly()
    {
        var metrics = new PerformanceMetrics
        {
            ElapsedTime = TimeSpan.FromSeconds(2),
            OperationCount = 100
        };

        Assert.Equal(50, metrics.OperationsPerSecond);
    }

    [Fact]
    public void OperationsPerSecond_should_return_zero_when_elapsed_time_is_zero()
    {
        var metrics = new PerformanceMetrics
        {
            ElapsedTime = TimeSpan.Zero,
            OperationCount = 100
        };

        Assert.Equal(0, metrics.OperationsPerSecond);
    }

    [Fact]
    public void AverageTimePerOperation_should_calculate_correctly()
    {
        var metrics = new PerformanceMetrics
        {
            ElapsedTime = TimeSpan.FromMilliseconds(100),
            OperationCount = 10
        };

        Assert.Equal(TimeSpan.FromMilliseconds(10), metrics.AverageTimePerOperation);
    }

    [Fact]
    public void AverageTimePerOperation_should_return_zero_when_operation_count_is_zero()
    {
        var metrics = new PerformanceMetrics
        {
            ElapsedTime = TimeSpan.FromMilliseconds(100),
            OperationCount = 0
        };

        Assert.Equal(TimeSpan.Zero, metrics.AverageTimePerOperation);
    }

    [Fact]
    public void AllocatedBytesPerOperation_should_calculate_correctly()
    {
        var metrics = new PerformanceMetrics
        {
            AllocatedBytes = 1000,
            OperationCount = 10
        };

        Assert.Equal(100.0, metrics.AllocatedBytesPerOperation);
    }

    [Fact]
    public void AllocatedBytesPerOperation_should_return_zero_when_operation_count_is_zero()
    {
        var metrics = new PerformanceMetrics
        {
            AllocatedBytes = 1000,
            OperationCount = 0
        };

        Assert.Equal(0.0, metrics.AllocatedBytesPerOperation);
    }

    [Fact]
    public void ToString_should_return_formatted_string()
    {
        var metrics = new PerformanceMetrics
        {
            ElapsedTime = TimeSpan.FromMilliseconds(150),
            OperationCount = 100,
            AllocatedBytes = 2048,
            Gen0Collections = 1,
            Gen1Collections = 0,
            Gen2Collections = 0
        };

        var result = metrics.ToString();

        Assert.Contains("150", result);
        Assert.Contains("Ops: 100", result);
        Assert.Contains("GC", result);
    }

    [Fact]
    public void ToString_should_format_large_bytes_as_KB()
    {
        var metrics = new PerformanceMetrics
        {
            AllocatedBytes = 10240 // 10 KB
        };

        var result = metrics.ToString();

        Assert.Contains("KB", result);
    }

    [Fact]
    public void ToString_should_format_very_large_bytes_as_MB()
    {
        var metrics = new PerformanceMetrics
        {
            AllocatedBytes = 10485760 // 10 MB
        };

        var result = metrics.ToString();

        Assert.Contains("MB", result);
    }
}
