using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Verification;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Core;

public class MigrationResultTests
{
    private static IMigrationProgress CreateMockProgress(TimeSpan elapsed = default)
    {
        var progress = Substitute.For<IMigrationProgress>();
        progress.Elapsed.Returns(elapsed == default ? TimeSpan.FromMinutes(5) : elapsed);
        return progress;
    }

    public class CreateSuccessMethod
    {
        [Fact]
        public void Should_create_successful_result()
        {
            // Arrange
            var migrationId = Guid.NewGuid();
            var progress = CreateMockProgress();
            var statistics = new MigrationStatistics { TotalEvents = 100 };

            // Act
            var result = MigrationResult.CreateSuccess(migrationId, progress, statistics);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(MigrationStatus.Completed, result.Status);
            Assert.Equal(migrationId, result.MigrationId);
        }

        [Fact]
        public void Should_have_no_error_message()
        {
            // Arrange
            var progress = CreateMockProgress();
            var statistics = new MigrationStatistics();

            // Act
            var result = MigrationResult.CreateSuccess(Guid.NewGuid(), progress, statistics);

            // Assert
            Assert.Null(result.ErrorMessage);
            Assert.Null(result.Exception);
        }

        [Fact]
        public void Should_include_progress()
        {
            // Arrange
            var progress = CreateMockProgress();
            var statistics = new MigrationStatistics();

            // Act
            var result = MigrationResult.CreateSuccess(Guid.NewGuid(), progress, statistics);

            // Assert
            Assert.Same(progress, result.Progress);
        }

        [Fact]
        public void Should_include_statistics()
        {
            // Arrange
            var progress = CreateMockProgress();
            var statistics = new MigrationStatistics
            {
                TotalEvents = 500,
                EventsTransformed = 500,
                AverageEventsPerSecond = 100
            };

            // Act
            var result = MigrationResult.CreateSuccess(Guid.NewGuid(), progress, statistics);

            // Assert
            Assert.Same(statistics, result.Statistics);
        }

        [Fact]
        public void Should_include_verification_result_when_provided()
        {
            // Arrange
            var progress = CreateMockProgress();
            var statistics = new MigrationStatistics();
            var verificationResult = Substitute.For<IVerificationResult>();

            // Act
            var result = MigrationResult.CreateSuccess(Guid.NewGuid(), progress, statistics, verificationResult);

            // Assert
            Assert.Same(verificationResult, result.VerificationResult);
        }

        [Fact]
        public void Should_set_duration_from_progress_elapsed()
        {
            // Arrange
            var elapsed = TimeSpan.FromMinutes(10);
            var progress = CreateMockProgress(elapsed);
            var statistics = new MigrationStatistics();

            // Act
            var result = MigrationResult.CreateSuccess(Guid.NewGuid(), progress, statistics);

            // Assert
            Assert.Equal(elapsed, result.Duration);
        }
    }

    public class CreateFailureMethod
    {
        [Fact]
        public void Should_create_failed_result()
        {
            // Arrange
            var migrationId = Guid.NewGuid();
            var progress = CreateMockProgress();
            var exception = new InvalidOperationException("Something went wrong");
            var statistics = new MigrationStatistics();

            // Act
            var result = MigrationResult.CreateFailure(migrationId, progress, exception, statistics);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(MigrationStatus.Failed, result.Status);
            Assert.Equal(migrationId, result.MigrationId);
        }

        [Fact]
        public void Should_include_error_message_from_exception()
        {
            // Arrange
            var progress = CreateMockProgress();
            var exception = new Exception("Test error message");
            var statistics = new MigrationStatistics();

            // Act
            var result = MigrationResult.CreateFailure(Guid.NewGuid(), progress, exception, statistics);

            // Assert
            Assert.Equal("Test error message", result.ErrorMessage);
        }

        [Fact]
        public void Should_include_exception()
        {
            // Arrange
            var progress = CreateMockProgress();
            var exception = new ArgumentException("Invalid argument");
            var statistics = new MigrationStatistics();

            // Act
            var result = MigrationResult.CreateFailure(Guid.NewGuid(), progress, exception, statistics);

            // Assert
            Assert.Same(exception, result.Exception);
        }

        [Fact]
        public void Should_include_progress()
        {
            // Arrange
            var progress = CreateMockProgress();
            var exception = new Exception("Error");
            var statistics = new MigrationStatistics();

            // Act
            var result = MigrationResult.CreateFailure(Guid.NewGuid(), progress, exception, statistics);

            // Assert
            Assert.Same(progress, result.Progress);
        }

        [Fact]
        public void Should_set_duration_from_progress_elapsed()
        {
            // Arrange
            var elapsed = TimeSpan.FromMinutes(3);
            var progress = CreateMockProgress(elapsed);
            var exception = new Exception("Error");
            var statistics = new MigrationStatistics();

            // Act
            var result = MigrationResult.CreateFailure(Guid.NewGuid(), progress, exception, statistics);

            // Assert
            Assert.Equal(elapsed, result.Duration);
        }
    }

    public class CreateDryRunMethod
    {
        [Fact]
        public void Should_create_dry_run_result()
        {
            // Arrange
            var migrationId = Guid.NewGuid();
            var progress = CreateMockProgress();
            var plan = Substitute.For<IMigrationPlan>();
            plan.IsFeasible.Returns(true);

            // Act
            var result = MigrationResult.CreateDryRun(migrationId, progress, plan);

            // Assert
            Assert.Equal(MigrationStatus.Completed, result.Status);
            Assert.Equal(migrationId, result.MigrationId);
        }

        [Fact]
        public void Should_set_success_based_on_plan_feasibility_true()
        {
            // Arrange
            var progress = CreateMockProgress();
            var plan = Substitute.For<IMigrationPlan>();
            plan.IsFeasible.Returns(true);

            // Act
            var result = MigrationResult.CreateDryRun(Guid.NewGuid(), progress, plan);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public void Should_set_success_based_on_plan_feasibility_false()
        {
            // Arrange
            var progress = CreateMockProgress();
            var plan = Substitute.For<IMigrationPlan>();
            plan.IsFeasible.Returns(false);

            // Act
            var result = MigrationResult.CreateDryRun(Guid.NewGuid(), progress, plan);

            // Assert
            Assert.False(result.Success);
        }

        [Fact]
        public void Should_include_plan()
        {
            // Arrange
            var progress = CreateMockProgress();
            var plan = Substitute.For<IMigrationPlan>();
            plan.IsFeasible.Returns(true);

            // Act
            var result = MigrationResult.CreateDryRun(Guid.NewGuid(), progress, plan);

            // Assert
            Assert.Same(plan, result.Plan);
        }

        [Fact]
        public void Should_have_empty_statistics()
        {
            // Arrange
            var progress = CreateMockProgress();
            var plan = Substitute.For<IMigrationPlan>();
            plan.IsFeasible.Returns(true);

            // Act
            var result = MigrationResult.CreateDryRun(Guid.NewGuid(), progress, plan);

            // Assert
            Assert.NotNull(result.Statistics);
        }
    }

    public class InterfaceImplementation
    {
        [Fact]
        public void Should_implement_IMigrationResult()
        {
            // Arrange
            var progress = CreateMockProgress();
            var statistics = new MigrationStatistics();

            // Act
            var sut = MigrationResult.CreateSuccess(Guid.NewGuid(), progress, statistics);

            // Assert
            Assert.IsAssignableFrom<IMigrationResult>(sut);
        }
    }
}

public class MigrationStatisticsTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_zero_values_by_default()
        {
            // Arrange & Act
            var sut = new MigrationStatistics();

            // Assert
            Assert.Equal(0, sut.TotalEvents);
            Assert.Equal(0, sut.EventsTransformed);
            Assert.Equal(0, sut.TransformationFailures);
            Assert.Equal(0, sut.AverageEventsPerSecond);
            Assert.Equal(0, sut.TotalBytes);
        }

        [Fact]
        public void Should_have_default_started_at()
        {
            // Arrange & Act
            var sut = new MigrationStatistics();

            // Assert
            Assert.Equal(default, sut.StartedAt);
        }

        [Fact]
        public void Should_have_null_completed_at()
        {
            // Arrange & Act
            var sut = new MigrationStatistics();

            // Assert
            Assert.Null(sut.CompletedAt);
        }
    }

    public class PropertySetters
    {
        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange
            var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            var completedAt = DateTimeOffset.UtcNow;

            // Act
            var sut = new MigrationStatistics
            {
                TotalEvents = 1000,
                EventsTransformed = 950,
                TransformationFailures = 50,
                AverageEventsPerSecond = 100.5,
                TotalBytes = 1024 * 1024,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };

            // Assert
            Assert.Equal(1000, sut.TotalEvents);
            Assert.Equal(950, sut.EventsTransformed);
            Assert.Equal(50, sut.TransformationFailures);
            Assert.Equal(100.5, sut.AverageEventsPerSecond);
            Assert.Equal(1024 * 1024, sut.TotalBytes);
            Assert.Equal(startedAt, sut.StartedAt);
            Assert.Equal(completedAt, sut.CompletedAt);
        }
    }
}
