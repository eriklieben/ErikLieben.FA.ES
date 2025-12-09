using ErikLieben.FA.ES.CLI.IO;

namespace ErikLieben.FA.ES.CLI.Tests.IO;

public class PerformanceTrackerTests
{
    public class TrackMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_return_disposable()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            var result = sut.Track("test");

            // Assert
            Assert.NotNull(result);
            result.Dispose();
        }

        [Fact]
        public async Task Should_track_duration_when_disposed()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            using (sut.Track("test"))
            {
                await Task.Delay(10); // Small delay to ensure measurable duration
            }

            // Assert
            var metrics = sut.GetMetrics();
            Assert.NotNull(metrics);
        }
    }

    public class TrackAnalysisMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_return_disposable()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            var result = sut.TrackAnalysis();

            // Assert
            Assert.NotNull(result);
            result.Dispose();
        }

        [Fact]
        public async Task Should_record_analysis_duration()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            using (sut.TrackAnalysis())
            {
                await Task.Delay(10);
            }
            var metrics = sut.GetMetrics();

            // Assert
            Assert.True(metrics.AnalysisDuration > TimeSpan.Zero);
        }
    }

    public class TrackGenerationMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_return_disposable()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            var result = sut.TrackGeneration();

            // Assert
            Assert.NotNull(result);
            result.Dispose();
        }

        [Fact]
        public async Task Should_record_generation_duration()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            using (sut.TrackGeneration())
            {
                await Task.Delay(10);
            }
            var metrics = sut.GetMetrics();

            // Assert
            Assert.True(metrics.GenerationDuration > TimeSpan.Zero);
        }
    }

    public class TrackFileWriteMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_return_disposable()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            var result = sut.TrackFileWrite();

            // Assert
            Assert.NotNull(result);
            result.Dispose();
        }

        [Fact]
        public async Task Should_record_file_write_duration()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            using (sut.TrackFileWrite())
            {
                await Task.Delay(10);
            }
            var metrics = sut.GetMetrics();

            // Assert
            Assert.True(metrics.FileWriteDuration > TimeSpan.Zero);
        }
    }

    public class RecordFileGeneratedMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_increment_files_generated_counter()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            sut.RecordFileGenerated();
            sut.RecordFileGenerated();
            sut.RecordFileGenerated();
            var metrics = sut.GetMetrics();

            // Assert
            Assert.Equal(3, metrics.FilesGenerated);
        }
    }

    public class RecordFileSkippedMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_increment_files_skipped_counter()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            sut.RecordFileSkipped();
            sut.RecordFileSkipped();
            var metrics = sut.GetMetrics();

            // Assert
            Assert.Equal(2, metrics.FilesSkipped);
        }
    }

    public class RecordProjectAnalyzedMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_increment_projects_analyzed_counter()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            sut.RecordProjectAnalyzed();
            sut.RecordProjectAnalyzed();
            sut.RecordProjectAnalyzed();
            sut.RecordProjectAnalyzed();
            var metrics = sut.GetMetrics();

            // Assert
            Assert.Equal(4, metrics.ProjectsAnalyzed);
        }
    }

    public class RecordMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_set_metric_value()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            sut.Record("customMetric", 42);

            // Assert - Record doesn't throw and metrics can be retrieved
            var metrics = sut.GetMetrics();
            Assert.NotNull(metrics);
        }
    }

    public class IncrementMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_increment_counter()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            sut.Increment("counter");
            sut.Increment("counter");
            sut.Increment("counter");

            // Assert - Increment doesn't throw and metrics can be retrieved
            var metrics = sut.GetMetrics();
            Assert.NotNull(metrics);
        }
    }

    public class GetMetricsMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_return_zero_values_for_fresh_tracker()
        {
            // Arrange
            var sut = new PerformanceTracker();

            // Act
            var metrics = sut.GetMetrics();

            // Assert
            Assert.Equal(TimeSpan.Zero, metrics.AnalysisDuration);
            Assert.Equal(TimeSpan.Zero, metrics.GenerationDuration);
            Assert.Equal(TimeSpan.Zero, metrics.FileWriteDuration);
            Assert.Equal(0, metrics.FilesGenerated);
            Assert.Equal(0, metrics.FilesSkipped);
            Assert.Equal(0, metrics.ProjectsAnalyzed);
        }

        [Fact]
        public async Task Should_return_in_progress_duration_for_running_operation()
        {
            // Arrange
            var sut = new PerformanceTracker();
            var scope = sut.TrackAnalysis();
            await Task.Delay(20);

            // Act
            var metrics = sut.GetMetrics();

            // Assert
            Assert.True(metrics.AnalysisDuration >= TimeSpan.FromMilliseconds(15));

            scope.Dispose();
        }
    }

    public class ResetMethod : PerformanceTrackerTests
    {
        [Fact]
        public void Should_clear_all_metrics()
        {
            // Arrange
            var sut = new PerformanceTracker();
            using (sut.TrackAnalysis()) { }
            sut.RecordFileGenerated();
            sut.RecordFileSkipped();
            sut.RecordProjectAnalyzed();

            // Act
            sut.Reset();
            var metrics = sut.GetMetrics();

            // Assert
            Assert.Equal(TimeSpan.Zero, metrics.AnalysisDuration);
            Assert.Equal(0, metrics.FilesGenerated);
            Assert.Equal(0, metrics.FilesSkipped);
            Assert.Equal(0, metrics.ProjectsAnalyzed);
        }
    }
}
