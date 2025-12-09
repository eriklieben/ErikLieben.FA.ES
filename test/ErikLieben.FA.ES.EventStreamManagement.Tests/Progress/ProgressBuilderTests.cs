using ErikLieben.FA.ES.EventStreamManagement.Progress;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Progress;

public class ProgressBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange & Act
            var sut = new ProgressBuilder();

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ReportEveryMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new ProgressBuilder();

            // Act
            var result = sut.ReportEvery(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(30)]
        [InlineData(60)]
        public void Should_accept_various_intervals(int seconds)
        {
            // Arrange
            var sut = new ProgressBuilder();

            // Act & Assert (no exception should be thrown)
            var result = sut.ReportEvery(TimeSpan.FromSeconds(seconds));
            Assert.Same(sut, result);
        }
    }

    public class OnProgressMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new ProgressBuilder();
            Action<IMigrationProgress> callback = _ => { };

            // Act
            var result = sut.OnProgress(callback);

            // Assert
            Assert.Same(sut, result);
        }

        [Fact]
        public void Should_accept_callback()
        {
            // Arrange
            var sut = new ProgressBuilder();
            var callbackInvoked = false;
            Action<IMigrationProgress> callback = _ => callbackInvoked = true;

            // Act
            sut.OnProgress(callback);

            // Assert - callback is stored, not invoked
            Assert.False(callbackInvoked);
        }
    }

    public class OnCompletedMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new ProgressBuilder();
            Action<IMigrationProgress> callback = _ => { };

            // Act
            var result = sut.OnCompleted(callback);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class OnFailedMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new ProgressBuilder();
            Action<IMigrationProgress, Exception> callback = (_, _) => { };

            // Act
            var result = sut.OnFailed(callback);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class EnableLoggingMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new ProgressBuilder();

            // Act
            var result = sut.EnableLogging();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class TrackCustomMetricMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new ProgressBuilder();

            // Act
            var result = sut.TrackCustomMetric("counter", () => 42);

            // Assert
            Assert.Same(sut, result);
        }

        [Fact]
        public void Should_accept_multiple_custom_metrics()
        {
            // Arrange
            var sut = new ProgressBuilder();

            // Act
            sut.TrackCustomMetric("metric1", () => 1)
               .TrackCustomMetric("metric2", () => "value")
               .TrackCustomMetric("metric3", () => new { X = 1 });

            // Assert - no exception should be thrown
            Assert.NotNull(sut);
        }
    }

    public class FluentChainingTests
    {
        [Fact]
        public void Should_support_full_fluent_configuration()
        {
            // Arrange
            var progressCallbackCalled = false;
            var completedCallbackCalled = false;
            var failedCallbackCalled = false;

            // Act
            var sut = new ProgressBuilder()
                .ReportEvery(TimeSpan.FromSeconds(10))
                .OnProgress(_ => progressCallbackCalled = true)
                .OnCompleted(_ => completedCallbackCalled = true)
                .OnFailed((_, _) => failedCallbackCalled = true)
                .EnableLogging()
                .TrackCustomMetric("testMetric", () => "test");

            // Assert
            Assert.NotNull(sut);
            Assert.False(progressCallbackCalled);
            Assert.False(completedCallbackCalled);
            Assert.False(failedCallbackCalled);
        }
    }
}
