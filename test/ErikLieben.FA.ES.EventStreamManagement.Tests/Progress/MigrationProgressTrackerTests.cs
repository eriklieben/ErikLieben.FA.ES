using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Progress;

public class MigrationProgressTrackerTests
{
    private static MigrationProgressTracker CreateTracker(
        Guid? migrationId = null,
        ProgressConfiguration? config = null,
        ILogger? logger = null)
    {
        return new MigrationProgressTracker(
            migrationId ?? Guid.NewGuid(),
            config,
            logger ?? Substitute.For<ILogger>());
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_is_null()
        {
            // Arrange
            ILogger? logger = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MigrationProgressTracker(Guid.NewGuid(), null, logger!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var logger = Substitute.For<ILogger>();

            // Act
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, logger);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_accept_null_config()
        {
            // Arrange
            var logger = Substitute.For<ILogger>();

            // Act
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, logger);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_initialize_with_pending_status()
        {
            // Arrange
            var logger = Substitute.For<ILogger>();

            // Act
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, logger);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(MigrationStatus.Pending, progress.Status);
        }

        [Fact]
        public void Should_initialize_with_normal_phase()
        {
            // Arrange
            var logger = Substitute.For<ILogger>();

            // Act
            var sut = new MigrationProgressTracker(Guid.NewGuid(), null, logger);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(MigrationPhase.Normal, progress.CurrentPhase);
        }
    }

    public class TotalEventsProperty
    {
        [Fact]
        public void Should_get_and_set_total_events()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.TotalEvents = 1000;

            // Assert
            Assert.Equal(1000, sut.TotalEvents);
        }
    }

    public class IncrementProcessedMethod
    {
        [Fact]
        public void Should_increment_by_one_by_default()
        {
            // Arrange
            var sut = CreateTracker();
            sut.TotalEvents = 100;

            // Act
            sut.IncrementProcessed();
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(1, progress.EventsProcessed);
        }

        [Fact]
        public void Should_increment_by_specified_count()
        {
            // Arrange
            var sut = CreateTracker();
            sut.TotalEvents = 100;

            // Act
            sut.IncrementProcessed(10);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(10, progress.EventsProcessed);
        }

        [Fact]
        public void Should_accumulate_increments()
        {
            // Arrange
            var sut = CreateTracker();
            sut.TotalEvents = 100;

            // Act
            sut.IncrementProcessed(5);
            sut.IncrementProcessed(3);
            sut.IncrementProcessed(2);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(10, progress.EventsProcessed);
        }
    }

    public class SetStatusMethod
    {
        [Fact]
        public void Should_update_status()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetStatus(MigrationStatus.InProgress);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(MigrationStatus.InProgress, progress.Status);
        }

        [Theory]
        [InlineData(MigrationStatus.Pending)]
        [InlineData(MigrationStatus.InProgress)]
        [InlineData(MigrationStatus.Paused)]
        [InlineData(MigrationStatus.Verifying)]
        [InlineData(MigrationStatus.Completed)]
        [InlineData(MigrationStatus.Failed)]
        [InlineData(MigrationStatus.Cancelled)]
        public void Should_accept_all_status_values(MigrationStatus status)
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetStatus(status);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(status, progress.Status);
        }
    }

    public class SetPhaseMethod
    {
        [Fact]
        public void Should_update_phase()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetPhase(MigrationPhase.DualWrite);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(MigrationPhase.DualWrite, progress.CurrentPhase);
        }

        [Theory]
        [InlineData(MigrationPhase.Normal)]
        [InlineData(MigrationPhase.DualWrite)]
        [InlineData(MigrationPhase.DualRead)]
        [InlineData(MigrationPhase.Cutover)]
        [InlineData(MigrationPhase.BookClosed)]
        public void Should_accept_all_phase_values(MigrationPhase phase)
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetPhase(phase);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(phase, progress.CurrentPhase);
        }
    }

    public class SetPausedMethod
    {
        [Fact]
        public void Should_set_is_paused_to_true()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetPaused(true);
            var progress = sut.GetProgress();

            // Assert
            Assert.True(progress.IsPaused);
        }

        [Fact]
        public void Should_set_is_paused_to_false()
        {
            // Arrange
            var sut = CreateTracker();
            sut.SetPaused(true);

            // Act
            sut.SetPaused(false);
            var progress = sut.GetProgress();

            // Assert
            Assert.False(progress.IsPaused);
        }
    }

    public class SetErrorMethod
    {
        [Fact]
        public void Should_set_error_message()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetError("Something went wrong");
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal("Something went wrong", progress.ErrorMessage);
        }

        [Fact]
        public void Should_set_status_to_failed()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetError("Error occurred");
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(MigrationStatus.Failed, progress.Status);
        }
    }

    public class SetCustomMetricMethod
    {
        [Fact]
        public void Should_add_custom_metric()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetCustomMetric("counter", 42);
            var progress = sut.GetProgress();

            // Assert
            Assert.True(progress.CustomMetrics.ContainsKey("counter"));
            Assert.Equal(42, progress.CustomMetrics["counter"]);
        }

        [Fact]
        public void Should_update_existing_metric()
        {
            // Arrange
            var sut = CreateTracker();
            sut.SetCustomMetric("counter", 1);

            // Act
            sut.SetCustomMetric("counter", 2);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(2, progress.CustomMetrics["counter"]);
        }

        [Fact]
        public void Should_support_multiple_metrics()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.SetCustomMetric("metric1", 1);
            sut.SetCustomMetric("metric2", "value");
            sut.SetCustomMetric("metric3", true);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(3, progress.CustomMetrics.Count);
        }
    }

    public class GetProgressMethod
    {
        [Fact]
        public void Should_return_migration_progress()
        {
            // Arrange
            var id = Guid.NewGuid();
            var sut = CreateTracker(id);

            // Act
            var result = sut.GetProgress();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(id, result.MigrationId);
        }

        [Fact]
        public void Should_calculate_percentage_complete()
        {
            // Arrange
            var sut = CreateTracker();
            sut.TotalEvents = 100;
            sut.IncrementProcessed(50);

            // Act
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(50.0, progress.PercentageComplete);
        }

        [Fact]
        public void Should_return_zero_percentage_when_no_total_events()
        {
            // Arrange
            var sut = CreateTracker();
            sut.TotalEvents = 0;
            sut.IncrementProcessed(10);

            // Act
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(0.0, progress.PercentageComplete);
        }

        [Fact]
        public async Task Should_return_elapsed_time()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            await Task.Delay(50); // Wait a bit
            var progress = sut.GetProgress();

            // Assert
            Assert.True(progress.Elapsed.TotalMilliseconds >= 40);
        }

        [Fact]
        public void Should_set_can_pause_to_true()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            var progress = sut.GetProgress();

            // Assert
            Assert.True(progress.CanPause);
        }

        [Fact]
        public void Should_set_can_rollback_to_true()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            var progress = sut.GetProgress();

            // Assert
            Assert.True(progress.CanRollback);
        }
    }

    public class ReportMethod
    {
        [Fact]
        public void Should_invoke_progress_callback_when_configured()
        {
            // Arrange
            var callbackInvoked = false;
            IMigrationProgress? receivedProgress = null;
            var config = new ProgressConfiguration
            {
                OnProgress = p =>
                {
                    callbackInvoked = true;
                    receivedProgress = p;
                }
            };
            var sut = CreateTracker(config: config);

            // Act
            sut.Report();

            // Assert
            Assert.True(callbackInvoked);
            Assert.NotNull(receivedProgress);
        }

        [Fact]
        public void Should_not_throw_when_no_config()
        {
            // Arrange
            var sut = CreateTracker();

            // Act & Assert
            var exception = Record.Exception(() => sut.Report());
            Assert.Null(exception);
        }
    }

    public class ReportCompletedMethod
    {
        [Fact]
        public void Should_set_status_to_completed()
        {
            // Arrange
            var sut = CreateTracker();

            // Act
            sut.ReportCompleted();
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(MigrationStatus.Completed, progress.Status);
        }

        [Fact]
        public void Should_invoke_completed_callback_when_configured()
        {
            // Arrange
            var callbackInvoked = false;
            var config = new ProgressConfiguration
            {
                OnCompleted = _ => callbackInvoked = true
            };
            var sut = CreateTracker(config: config);

            // Act
            sut.ReportCompleted();

            // Assert
            Assert.True(callbackInvoked);
        }
    }

    public class ReportFailedMethod
    {
        [Fact]
        public void Should_set_status_to_failed()
        {
            // Arrange
            var sut = CreateTracker();
            var exception = new Exception("Test error");

            // Act
            sut.ReportFailed(exception);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal(MigrationStatus.Failed, progress.Status);
        }

        [Fact]
        public void Should_set_error_message_from_exception()
        {
            // Arrange
            var sut = CreateTracker();
            var exception = new Exception("Test error message");

            // Act
            sut.ReportFailed(exception);
            var progress = sut.GetProgress();

            // Assert
            Assert.Equal("Test error message", progress.ErrorMessage);
        }

        [Fact]
        public void Should_invoke_failed_callback_when_configured()
        {
            // Arrange
            var callbackInvoked = false;
            Exception? receivedException = null;
            var config = new ProgressConfiguration
            {
                OnFailed = (_, ex) =>
                {
                    callbackInvoked = true;
                    receivedException = ex;
                }
            };
            var sut = CreateTracker(config: config);
            var exception = new InvalidOperationException("Test");

            // Act
            sut.ReportFailed(exception);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Same(exception, receivedException);
        }
    }

    public class CustomMetricsFromConfigTests
    {
        [Fact]
        public void Should_collect_custom_metrics_from_config()
        {
            // Arrange
            var counter = 0;
            var config = new ProgressConfiguration();
            config.CustomMetrics["counter"] = () => ++counter;
            var sut = CreateTracker(config: config);

            // Act
            var progress1 = sut.GetProgress();
            var progress2 = sut.GetProgress();

            // Assert
            Assert.Equal(1, progress1.CustomMetrics["counter"]);
            Assert.Equal(2, progress2.CustomMetrics["counter"]);
        }
    }
}
