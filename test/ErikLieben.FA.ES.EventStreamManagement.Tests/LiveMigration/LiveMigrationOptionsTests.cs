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

        [Fact]
        public void Should_have_null_event_copied_callback_by_default()
        {
            // Act
            var sut = new LiveMigrationOptions();

            // Assert
            Assert.Null(sut.EventCopiedCallback);
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

    public class OnEventCopiedMethod
    {
        [Fact]
        public async Task Should_set_event_copied_callback()
        {
            // Arrange
            var sut = new LiveMigrationOptions();
            var callbackInvoked = false;
            Func<LiveMigrationEventProgress, Task> callback = _ =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            var result = sut.OnEventCopied(callback);

            // Assert
            Assert.Same(sut, result);
            Assert.NotNull(sut.EventCopiedCallback);

            // Verify callback can be invoked
            await sut.EventCopiedCallback(new LiveMigrationEventProgress
            {
                Iteration = 1,
                EventVersion = 5,
                EventType = "TestEvent",
                WasTransformed = false,
                TotalEventsCopied = 5,
                SourceVersion = 10,
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
            Assert.Throws<ArgumentNullException>(() => sut.OnEventCopied(null!));
        }

        [Fact]
        public async Task Should_receive_transformation_details_when_event_was_transformed()
        {
            // Arrange
            var sut = new LiveMigrationOptions();
            LiveMigrationEventProgress? receivedProgress = null;
            sut.OnEventCopied(p =>
            {
                receivedProgress = p;
                return Task.CompletedTask;
            });

            // Act
            await sut.EventCopiedCallback!(new LiveMigrationEventProgress
            {
                Iteration = 2,
                EventVersion = 3,
                EventType = "NewEventType",
                WasTransformed = true,
                OriginalEventType = "OldEventType",
                OriginalSchemaVersion = 1,
                NewSchemaVersion = 2,
                TotalEventsCopied = 10,
                SourceVersion = 20,
                ElapsedTime = TimeSpan.FromSeconds(5)
            });

            // Assert
            Assert.NotNull(receivedProgress);
            Assert.True(receivedProgress.WasTransformed);
            Assert.Equal("OldEventType", receivedProgress.OriginalEventType);
            Assert.Equal(1, receivedProgress.OriginalSchemaVersion);
            Assert.Equal(2, receivedProgress.NewSchemaVersion);
            Assert.Equal("NewEventType", receivedProgress.EventType);
        }
    }

    public class OnBeforeAppendMethod
    {
        [Fact]
        public async Task Should_set_before_append_callback()
        {
            // Arrange
            var sut = new LiveMigrationOptions();
            var callbackInvoked = false;
            Func<LiveMigrationEventProgress, Task> callback = _ =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            var result = sut.OnBeforeAppend(callback);

            // Assert
            Assert.Same(sut, result);
            Assert.NotNull(sut.BeforeAppendCallback);

            // Verify callback can be invoked
            await sut.BeforeAppendCallback(new LiveMigrationEventProgress
            {
                Iteration = 1,
                EventVersion = 5,
                EventType = "TestEvent",
                WasTransformed = false,
                TotalEventsCopied = 5,
                SourceVersion = 10,
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
            Assert.Throws<ArgumentNullException>(() => sut.OnBeforeAppend(null!));
        }

        [Fact]
        public async Task Should_support_async_delay_in_callback()
        {
            // Arrange
            var sut = new LiveMigrationOptions();
            var delayMs = 50;
            var stopwatch = new System.Diagnostics.Stopwatch();

            sut.OnBeforeAppend(async _ =>
            {
                await Task.Delay(delayMs);
            });

            // Act
            stopwatch.Start();
            await sut.BeforeAppendCallback!(new LiveMigrationEventProgress
            {
                Iteration = 1,
                EventVersion = 1,
                EventType = "TestEvent",
                WasTransformed = false,
                TotalEventsCopied = 1,
                SourceVersion = 10,
                ElapsedTime = TimeSpan.Zero
            });
            stopwatch.Stop();

            // Assert - should have taken at least the delay time
            Assert.True(stopwatch.ElapsedMilliseconds >= delayMs - 10); // Allow 10ms tolerance
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

        [Fact]
        public void Should_support_fluent_configuration_with_event_copied_callback()
        {
            // Arrange & Act
            var progressReceived = new List<LiveMigrationProgress>();
            var eventsReceived = new List<LiveMigrationEventProgress>();

            var sut = new LiveMigrationOptions();
            sut.WithCloseTimeout(TimeSpan.FromMinutes(10))
               .WithCatchUpDelay(TimeSpan.FromMilliseconds(200))
               .OnCatchUpProgress(p => progressReceived.Add(p))
               .OnEventCopied(e =>
               {
                   eventsReceived.Add(e);
                   return Task.CompletedTask;
               })
               .OnConvergenceFailure(ConvergenceFailureStrategy.Fail)
               .WithMaxIterations(50);

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(10), sut.CloseTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(200), sut.CatchUpDelay);
            Assert.NotNull(sut.ProgressCallback);
            Assert.NotNull(sut.EventCopiedCallback);
            Assert.Equal(ConvergenceFailureStrategy.Fail, sut.FailureStrategy);
            Assert.Equal(50, sut.MaxIterations);
        }

        [Fact]
        public void Should_support_fluent_configuration_with_before_append_callback()
        {
            // Arrange & Act
            var progressReceived = new List<LiveMigrationProgress>();
            var eventsReceived = new List<LiveMigrationEventProgress>();
            var beforeAppendCount = 0;

            var sut = new LiveMigrationOptions();
            sut.WithCloseTimeout(TimeSpan.FromMinutes(10))
               .WithCatchUpDelay(TimeSpan.FromMilliseconds(200))
               .OnCatchUpProgress(p => progressReceived.Add(p))
               .OnBeforeAppend(async _ =>
               {
                   beforeAppendCount++;
                   await Task.Delay(10); // Simulate async work
               })
               .OnEventCopied(e =>
               {
                   eventsReceived.Add(e);
                   return Task.CompletedTask;
               })
               .OnConvergenceFailure(ConvergenceFailureStrategy.Fail)
               .WithMaxIterations(50);

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(10), sut.CloseTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(200), sut.CatchUpDelay);
            Assert.NotNull(sut.ProgressCallback);
            Assert.NotNull(sut.BeforeAppendCallback);
            Assert.NotNull(sut.EventCopiedCallback);
            Assert.Equal(ConvergenceFailureStrategy.Fail, sut.FailureStrategy);
            Assert.Equal(50, sut.MaxIterations);
        }
    }
}
