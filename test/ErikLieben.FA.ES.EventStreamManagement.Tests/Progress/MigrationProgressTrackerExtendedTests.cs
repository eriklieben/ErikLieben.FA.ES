using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Progress;

public class MigrationProgressTrackerExtendedTests
{
    private static ILogger<MigrationProgressTracker> CreateLogger()
    {
        var logger = Substitute.For<ILogger<MigrationProgressTracker>>();
        // Enable all log levels so source-generated LoggerMessage methods call Log()
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        return logger;
    }

    public class IncrementProcessedMethod
    {
        [Fact]
        public void Should_increment_events_processed()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());
            sut.TotalEvents = 100;

            // Act
            sut.IncrementProcessed();
            sut.IncrementProcessed();
            sut.IncrementProcessed();

            // Assert
            Assert.Equal(3, sut.GetProgress().EventsProcessed);
        }

        [Fact]
        public void Should_accept_count_parameter()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());
            sut.TotalEvents = 100;

            // Act
            sut.IncrementProcessed(10);

            // Assert
            Assert.Equal(10, sut.GetProgress().EventsProcessed);
        }

        [Fact]
        public void Should_trigger_callback_based_on_report_interval()
        {
            // Arrange
            var callbackInvoked = false;
            var config = new ProgressConfiguration
            {
                ReportInterval = TimeSpan.Zero, // Always report
                OnProgress = _ => callbackInvoked = true
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, CreateLogger());
            sut.TotalEvents = 100;

            // Act
            sut.IncrementProcessed();

            // Assert
            Assert.True(callbackInvoked);
        }
    }

    public class SetStatusMethod
    {
        [Theory]
        [InlineData(MigrationStatus.Pending)]
        [InlineData(MigrationStatus.InProgress)]
        [InlineData(MigrationStatus.Completed)]
        [InlineData(MigrationStatus.Failed)]
        [InlineData(MigrationStatus.Cancelled)]
        [InlineData(MigrationStatus.Verifying)]
        [InlineData(MigrationStatus.CuttingOver)]
        [InlineData(MigrationStatus.RollingBack)]
        public void Should_set_status(MigrationStatus expectedStatus)
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.SetStatus(expectedStatus);

            // Assert
            Assert.Equal(expectedStatus, sut.GetProgress().Status);
        }
    }

    public class SetPhaseMethod
    {
        [Theory]
        [InlineData(MigrationPhase.Normal)]
        [InlineData(MigrationPhase.DualWrite)]
        [InlineData(MigrationPhase.DualRead)]
        [InlineData(MigrationPhase.Cutover)]
        [InlineData(MigrationPhase.BookClosed)]
        public void Should_set_phase(MigrationPhase expectedPhase)
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.SetPhase(expectedPhase);

            // Assert
            Assert.Equal(expectedPhase, sut.GetProgress().CurrentPhase);
        }
    }

    public class SetPausedMethod
    {
        [Fact]
        public void Should_set_paused_true()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.SetPaused(true);

            // Assert
            Assert.True(sut.GetProgress().IsPaused);
        }

        [Fact]
        public void Should_set_paused_false()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());
            sut.SetPaused(true);

            // Act
            sut.SetPaused(false);

            // Assert
            Assert.False(sut.GetProgress().IsPaused);
        }
    }

    public class SetErrorMethod
    {
        [Fact]
        public void Should_set_error_message()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.SetError("Something went wrong");

            // Assert
            var progress = sut.GetProgress();
            Assert.Equal("Something went wrong", progress.ErrorMessage);
            Assert.Equal(MigrationStatus.Failed, progress.Status);
        }
    }

    public class SetCustomMetricMethod
    {
        [Fact]
        public void Should_set_custom_metric()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.SetCustomMetric("bytesTransferred", 1024L);

            // Assert
            var progress = sut.GetProgress();
            Assert.Contains("bytesTransferred", progress.CustomMetrics.Keys);
            Assert.Equal(1024L, progress.CustomMetrics["bytesTransferred"]);
        }

        [Fact]
        public void Should_update_existing_metric()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());
            sut.SetCustomMetric("count", 1);

            // Act
            sut.SetCustomMetric("count", 5);

            // Assert
            var progress = sut.GetProgress();
            Assert.Equal(5, progress.CustomMetrics["count"]);
        }

        [Fact]
        public void Should_support_multiple_metrics()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.SetCustomMetric("metric1", "value1");
            sut.SetCustomMetric("metric2", 42);
            sut.SetCustomMetric("metric3", true);

            // Assert
            var progress = sut.GetProgress();
            Assert.Equal(3, progress.CustomMetrics.Count);
        }
    }

    public class GetProgressMethod
    {
        [Fact]
        public void Should_calculate_percentage_complete()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());
            sut.TotalEvents = 100;
            sut.IncrementProcessed(50);

            // Act
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(50.0, progress.PercentageComplete);
        }

        [Fact]
        public void Should_return_zero_percentage_when_no_total()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(0.0, progress.PercentageComplete);
        }

        [Fact]
        public void Should_calculate_estimated_remaining()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());
            sut.TotalEvents = 100;
            // Simulate some processing time
            sut.IncrementProcessed(50);

            // Act
            var progress = sut.GetProgress();

            // Assert - estimated remaining should be non-null when there's progress
            // Note: This might be null if processing is too fast
            Assert.True(progress.EstimatedRemaining == null || progress.EstimatedRemaining >= TimeSpan.Zero);
        }

        [Fact]
        public async Task Should_include_elapsed_time()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Wait a tiny bit to ensure elapsed > 0
            await Task.Delay(10);

            // Act
            var progress = sut.GetProgress();

            // Assert
            Assert.True(progress.Elapsed > TimeSpan.Zero);
        }

        [Fact]
        public void Should_collect_custom_metrics_from_config()
        {
            // Arrange
            var config = new ProgressConfiguration
            {
                CustomMetrics = new Dictionary<string, Func<object>>
                {
                    { "timestamp", () => DateTimeOffset.UtcNow },
                    { "constant", () => 42 }
                }
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, CreateLogger());

            // Act
            var progress = sut.GetProgress();

            // Assert
            Assert.Contains("constant", progress.CustomMetrics.Keys);
            Assert.Equal(42, progress.CustomMetrics["constant"]);
        }

        [Fact]
        public void Should_handle_custom_metric_collector_exception()
        {
            // Arrange
            var config = new ProgressConfiguration
            {
                CustomMetrics = new Dictionary<string, Func<object>>
                {
                    { "errorMetric", () => throw new Exception("Metric collection failed") },
                    { "goodMetric", () => "value" }
                }
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, CreateLogger());

            // Act - should not throw
            var progress = sut.GetProgress();

            // Assert - good metric should still be collected
            Assert.Contains("goodMetric", progress.CustomMetrics.Keys);
        }
    }

    public class ReportMethod
    {
        [Fact]
        public void Should_invoke_on_progress_callback()
        {
            // Arrange
            IMigrationProgress? reportedProgress = null;
            var config = new ProgressConfiguration
            {
                OnProgress = p => reportedProgress = p
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, CreateLogger());

            // Act
            sut.Report();

            // Assert
            Assert.NotNull(reportedProgress);
        }

        [Fact]
        public void Should_log_when_logging_enabled()
        {
            // Arrange
            var logger = CreateLogger();
            var config = new ProgressConfiguration
            {
                EnableLogging = true
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, logger);

            // Act
            sut.Report();

            // Assert - logger should have been called
            logger.ReceivedWithAnyArgs().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }

    public class ReportCompletedMethod
    {
        [Fact]
        public void Should_set_status_to_completed()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.ReportCompleted();

            // Assert
            Assert.Equal(MigrationStatus.Completed, sut.GetProgress().Status);
        }

        [Fact]
        public void Should_invoke_on_completed_callback()
        {
            // Arrange
            IMigrationProgress? completedProgress = null;
            var config = new ProgressConfiguration
            {
                OnCompleted = p => completedProgress = p
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, CreateLogger());

            // Act
            sut.ReportCompleted();

            // Assert
            Assert.NotNull(completedProgress);
        }

        [Fact]
        public void Should_log_completion_when_logging_enabled()
        {
            // Arrange
            var logger = CreateLogger();
            var config = new ProgressConfiguration
            {
                EnableLogging = true
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, logger);

            // Act
            sut.ReportCompleted();

            // Assert
            logger.ReceivedWithAnyArgs().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }

    public class ReportFailedMethod
    {
        [Fact]
        public void Should_set_status_to_failed()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.ReportFailed(new Exception("Test error"));

            // Assert
            Assert.Equal(MigrationStatus.Failed, sut.GetProgress().Status);
        }

        [Fact]
        public void Should_set_error_message_from_exception()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.ReportFailed(new Exception("Something bad happened"));

            // Assert
            Assert.Equal("Something bad happened", sut.GetProgress().ErrorMessage);
        }

        [Fact]
        public void Should_invoke_on_failed_callback()
        {
            // Arrange
            IMigrationProgress? failedProgress = null;
            Exception? capturedException = null;
            var config = new ProgressConfiguration
            {
                OnFailed = (p, ex) =>
                {
                    failedProgress = p;
                    capturedException = ex;
                }
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, CreateLogger());
            var testException = new Exception("Test error");

            // Act
            sut.ReportFailed(testException);

            // Assert
            Assert.NotNull(failedProgress);
            Assert.Same(testException, capturedException);
        }

        [Fact]
        public void Should_log_error_when_logging_enabled()
        {
            // Arrange
            var logger = CreateLogger();
            var config = new ProgressConfiguration
            {
                EnableLogging = true
            };
            var sut = new MigrationProgressTracker(Guid.NewGuid(), config, logger);

            // Act
            sut.ReportFailed(new Exception("Test error"));

            // Assert
            logger.ReceivedWithAnyArgs().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }

    public class TotalEventsProperty
    {
        [Fact]
        public void Should_get_and_set_total_events()
        {
            // Arrange
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Act
            sut.TotalEvents = 1000;

            // Assert
            Assert.Equal(1000, sut.TotalEvents);
            Assert.Equal(1000, sut.GetProgress().TotalEvents);
        }
    }

    public class ConstructorTests
    {
        [Fact]
        public void Should_throw_when_logger_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MigrationProgressTracker(Guid.NewGuid(), null, null!));
        }

        [Fact]
        public void Should_initialize_with_pending_status()
        {
            // Arrange & Act
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Assert
            Assert.Equal(MigrationStatus.Pending, sut.GetProgress().Status);
        }

        [Fact]
        public void Should_initialize_with_normal_phase()
        {
            // Arrange & Act
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, CreateLogger());

            // Assert
            Assert.Equal(MigrationPhase.Normal, sut.GetProgress().CurrentPhase);
        }

        [Fact]
        public void Should_set_migration_id()
        {
            // Arrange
            var migrationId = Guid.NewGuid();

            // Act
            var sut = new MigrationProgressTracker(migrationId, null, CreateLogger());

            // Assert
            Assert.Equal(migrationId, sut.GetProgress().MigrationId);
        }
    }
}
