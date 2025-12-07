using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Cutover;
using ErikLieben.FA.ES.EventStreamManagement.Progress;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Progress;

public class MigrationProgressTests
{
    public class Properties
    {
        [Fact]
        public void Should_have_default_values()
        {
            // Arrange & Act
            var sut = new MigrationProgress();

            // Assert
            Assert.Equal(Guid.Empty, sut.MigrationId);
            Assert.Equal(default, sut.Status);
            Assert.Equal(default, sut.CurrentPhase);
            Assert.Equal(0, sut.PercentageComplete);
            Assert.Equal(0, sut.EventsProcessed);
            Assert.Equal(0, sut.TotalEvents);
            Assert.Equal(0, sut.EventsPerSecond);
            Assert.Equal(TimeSpan.Zero, sut.Elapsed);
            Assert.Null(sut.EstimatedRemaining);
            Assert.False(sut.IsPaused);
            Assert.False(sut.CanPause);
            Assert.False(sut.CanRollback);
            Assert.NotNull(sut.CustomMetrics);
            Assert.Empty(sut.CustomMetrics);
            Assert.Null(sut.ErrorMessage);
        }

        [Fact]
        public void Should_allow_setting_migration_id()
        {
            // Arrange
            var id = Guid.NewGuid();

            // Act
            var sut = new MigrationProgress { MigrationId = id };

            // Assert
            Assert.Equal(id, sut.MigrationId);
        }

        [Fact]
        public void Should_allow_setting_status()
        {
            // Arrange & Act
            var sut = new MigrationProgress { Status = MigrationStatus.InProgress };

            // Assert
            Assert.Equal(MigrationStatus.InProgress, sut.Status);
        }

        [Fact]
        public void Should_allow_setting_current_phase()
        {
            // Arrange & Act
            var sut = new MigrationProgress { CurrentPhase = MigrationPhase.DualWrite };

            // Assert
            Assert.Equal(MigrationPhase.DualWrite, sut.CurrentPhase);
        }

        [Fact]
        public void Should_allow_setting_percentage_complete()
        {
            // Arrange & Act
            var sut = new MigrationProgress { PercentageComplete = 75.5 };

            // Assert
            Assert.Equal(75.5, sut.PercentageComplete);
        }

        [Fact]
        public void Should_allow_setting_events_processed()
        {
            // Arrange & Act
            var sut = new MigrationProgress { EventsProcessed = 1000 };

            // Assert
            Assert.Equal(1000, sut.EventsProcessed);
        }

        [Fact]
        public void Should_allow_setting_total_events()
        {
            // Arrange & Act
            var sut = new MigrationProgress { TotalEvents = 5000 };

            // Assert
            Assert.Equal(5000, sut.TotalEvents);
        }

        [Fact]
        public void Should_allow_setting_events_per_second()
        {
            // Arrange & Act
            var sut = new MigrationProgress { EventsPerSecond = 250.5 };

            // Assert
            Assert.Equal(250.5, sut.EventsPerSecond);
        }

        [Fact]
        public void Should_allow_setting_elapsed()
        {
            // Arrange
            var elapsed = TimeSpan.FromMinutes(5);

            // Act
            var sut = new MigrationProgress { Elapsed = elapsed };

            // Assert
            Assert.Equal(elapsed, sut.Elapsed);
        }

        [Fact]
        public void Should_allow_setting_estimated_remaining()
        {
            // Arrange
            var estimated = TimeSpan.FromMinutes(10);

            // Act
            var sut = new MigrationProgress { EstimatedRemaining = estimated };

            // Assert
            Assert.Equal(estimated, sut.EstimatedRemaining);
        }

        [Fact]
        public void Should_allow_setting_is_paused()
        {
            // Arrange & Act
            var sut = new MigrationProgress { IsPaused = true };

            // Assert
            Assert.True(sut.IsPaused);
        }

        [Fact]
        public void Should_allow_setting_can_pause()
        {
            // Arrange & Act
            var sut = new MigrationProgress { CanPause = true };

            // Assert
            Assert.True(sut.CanPause);
        }

        [Fact]
        public void Should_allow_setting_can_rollback()
        {
            // Arrange & Act
            var sut = new MigrationProgress { CanRollback = true };

            // Assert
            Assert.True(sut.CanRollback);
        }

        [Fact]
        public void Should_allow_setting_custom_metrics()
        {
            // Arrange
            var metrics = new Dictionary<string, object> { { "key", "value" } };

            // Act
            var sut = new MigrationProgress { CustomMetrics = metrics };

            // Assert
            Assert.Same(metrics, sut.CustomMetrics);
        }

        [Fact]
        public void Should_allow_setting_error_message()
        {
            // Arrange & Act
            var sut = new MigrationProgress { ErrorMessage = "Something went wrong" };

            // Assert
            Assert.Equal("Something went wrong", sut.ErrorMessage);
        }
    }

    public class InterfaceImplementation
    {
        [Fact]
        public void Should_implement_IMigrationProgress()
        {
            // Arrange & Act
            var sut = new MigrationProgress();

            // Assert
            Assert.IsAssignableFrom<IMigrationProgress>(sut);
        }
    }

    public class FullProgressScenario
    {
        [Fact]
        public void Should_represent_complete_migration_progress()
        {
            // Arrange
            var id = Guid.NewGuid();
            var customMetrics = new Dictionary<string, object>
            {
                { "transformationErrors", 5 },
                { "skippedEvents", 2 }
            };

            // Act
            var sut = new MigrationProgress
            {
                MigrationId = id,
                Status = MigrationStatus.InProgress,
                CurrentPhase = MigrationPhase.DualWrite,
                PercentageComplete = 65.0,
                EventsProcessed = 6500,
                TotalEvents = 10000,
                EventsPerSecond = 500.0,
                Elapsed = TimeSpan.FromMinutes(13),
                EstimatedRemaining = TimeSpan.FromMinutes(7),
                IsPaused = false,
                CanPause = true,
                CanRollback = true,
                CustomMetrics = customMetrics,
                ErrorMessage = null
            };

            // Assert
            Assert.Equal(id, sut.MigrationId);
            Assert.Equal(MigrationStatus.InProgress, sut.Status);
            Assert.Equal(MigrationPhase.DualWrite, sut.CurrentPhase);
            Assert.Equal(65.0, sut.PercentageComplete);
            Assert.Equal(6500, sut.EventsProcessed);
            Assert.Equal(10000, sut.TotalEvents);
            Assert.Equal(500.0, sut.EventsPerSecond);
            Assert.Equal(TimeSpan.FromMinutes(13), sut.Elapsed);
            Assert.Equal(TimeSpan.FromMinutes(7), sut.EstimatedRemaining);
            Assert.False(sut.IsPaused);
            Assert.True(sut.CanPause);
            Assert.True(sut.CanRollback);
            Assert.Equal(2, sut.CustomMetrics.Count);
            Assert.Null(sut.ErrorMessage);
        }
    }
}
