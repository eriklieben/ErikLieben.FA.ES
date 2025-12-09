using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.LiveMigration;

public class LiveMigrationOptionsTests
{
    public class Constructor
    {
        [Fact]
        public void Should_have_default_close_timeout_of_5_minutes()
        {
            // Act
            var sut = new LiveMigrationOptions();

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(5), sut.CloseTimeout);
        }

        [Fact]
        public void Should_have_default_catch_up_delay_of_100ms()
        {
            // Act
            var sut = new LiveMigrationOptions();

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(100), sut.CatchUpDelay);
        }

        [Fact]
        public void Should_have_default_failure_strategy_of_KeepTrying()
        {
            // Act
            var sut = new LiveMigrationOptions();

            // Assert
            Assert.Equal(ConvergenceFailureStrategy.KeepTrying, sut.FailureStrategy);
        }

        [Fact]
        public void Should_have_default_max_iterations_of_0()
        {
            // Act
            var sut = new LiveMigrationOptions();

            // Assert
            Assert.Equal(0, sut.MaxIterations);
        }

        [Fact]
        public void Should_have_null_progress_callback_by_default()
        {
            // Act
            var sut = new LiveMigrationOptions();

            // Assert
            Assert.Null(sut.ProgressCallback);
        }
    }

    public class WithCloseTimeoutMethod
    {
        [Fact]
        public void Should_set_close_timeout()
        {
            // Arrange
            var sut = new LiveMigrationOptions();
            var timeout = TimeSpan.FromMinutes(10);

            // Act
            var result = sut.WithCloseTimeout(timeout);

            // Assert
            Assert.Same(sut, result);
            Assert.Equal(timeout, sut.CloseTimeout);
        }

        [Fact]
        public void Should_throw_when_timeout_is_zero()
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.WithCloseTimeout(TimeSpan.Zero));
        }

        [Fact]
        public void Should_throw_when_timeout_is_negative()
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.WithCloseTimeout(TimeSpan.FromSeconds(-1)));
        }
    }

    public class WithCatchUpDelayMethod
    {
        [Fact]
        public void Should_set_catch_up_delay()
        {
            // Arrange
            var sut = new LiveMigrationOptions();
            var delay = TimeSpan.FromMilliseconds(500);

            // Act
            var result = sut.WithCatchUpDelay(delay);

            // Assert
            Assert.Same(sut, result);
            Assert.Equal(delay, sut.CatchUpDelay);
        }

        [Fact]
        public void Should_allow_zero_delay()
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act
            var result = sut.WithCatchUpDelay(TimeSpan.Zero);

            // Assert
            Assert.Equal(TimeSpan.Zero, sut.CatchUpDelay);
        }

        [Fact]
        public void Should_throw_when_delay_is_negative()
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.WithCatchUpDelay(TimeSpan.FromSeconds(-1)));
        }
    }

    public class OnCatchUpProgressMethod
    {
        [Fact]
        public void Should_set_progress_callback()
        {
            // Arrange
            var sut = new LiveMigrationOptions();
            var callbackInvoked = false;
            Action<LiveMigrationProgress> callback = _ => callbackInvoked = true;

            // Act
            var result = sut.OnCatchUpProgress(callback);

            // Assert
            Assert.Same(sut, result);
            Assert.NotNull(sut.ProgressCallback);

            // Verify callback can be invoked
            sut.ProgressCallback(new LiveMigrationProgress
            {
                Iteration = 1,
                SourceVersion = 10,
                TargetVersion = 5,
                EventsCopiedThisIteration = 5,
                TotalEventsCopied = 5,
                ElapsedTime = TimeSpan.FromSeconds(1)
            });
            Assert.True(callbackInvoked);
        }

        [Fact]
        public void Should_throw_when_callback_is_null()
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.OnCatchUpProgress(null!));
        }
    }

    public class OnConvergenceFailureMethod
    {
        [Theory]
        [InlineData(ConvergenceFailureStrategy.KeepTrying)]
        [InlineData(ConvergenceFailureStrategy.Fail)]
        public void Should_set_failure_strategy(ConvergenceFailureStrategy strategy)
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act
            var result = sut.OnConvergenceFailure(strategy);

            // Assert
            Assert.Same(sut, result);
            Assert.Equal(strategy, sut.FailureStrategy);
        }
    }

    public class WithMaxIterationsMethod
    {
        [Fact]
        public void Should_set_max_iterations()
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act
            var result = sut.WithMaxIterations(100);

            // Assert
            Assert.Same(sut, result);
            Assert.Equal(100, sut.MaxIterations);
        }

        [Fact]
        public void Should_allow_zero_for_unlimited()
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act
            var result = sut.WithMaxIterations(0);

            // Assert
            Assert.Equal(0, sut.MaxIterations);
        }

        [Fact]
        public void Should_throw_when_max_iterations_is_negative()
        {
            // Arrange
            var sut = new LiveMigrationOptions();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.WithMaxIterations(-1));
        }
    }

    public class FluentChaining
    {
        [Fact]
        public void Should_support_fluent_configuration()
        {
            // Arrange & Act
            var progressReceived = new List<LiveMigrationProgress>();

            var sut = new LiveMigrationOptions();
            sut.WithCloseTimeout(TimeSpan.FromMinutes(10))
               .WithCatchUpDelay(TimeSpan.FromMilliseconds(200))
               .OnCatchUpProgress(p => progressReceived.Add(p))
               .OnConvergenceFailure(ConvergenceFailureStrategy.Fail)
               .WithMaxIterations(50);

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(10), sut.CloseTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(200), sut.CatchUpDelay);
            Assert.NotNull(sut.ProgressCallback);
            Assert.Equal(ConvergenceFailureStrategy.Fail, sut.FailureStrategy);
            Assert.Equal(50, sut.MaxIterations);
        }
    }
}
