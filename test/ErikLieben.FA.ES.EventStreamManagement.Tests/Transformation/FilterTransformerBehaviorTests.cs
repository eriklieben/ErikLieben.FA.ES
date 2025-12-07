using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Transformation;

/// <summary>
/// Tests for FilterTransformer behavior through the TransformationPipelineBuilder.
/// FilterTransformer is a private nested class, so we test its behavior through the public API.
/// </summary>
public class FilterTransformerBehaviorTests
{
    private static ILoggerFactory CreateLoggerFactory()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<TransformationPipeline>().Returns(Substitute.For<ILogger<TransformationPipeline>>());
        return loggerFactory;
    }

    private static IEvent CreateMockEvent(string eventType = "TestEvent", int version = 1)
    {
        var mockEvent = Substitute.For<IEvent>();
        mockEvent.EventType.Returns(eventType);
        mockEvent.EventVersion.Returns(version);
        return mockEvent;
    }

    public class FilterPassingTests
    {
        [Fact]
        public async Task Should_pass_event_through_when_filter_returns_true()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);
            builder.AddFilter(e => true); // Filter always passes

            var mockEvent = CreateMockEvent();

            var pipeline = builder.Build();

            // Act
            var result = await pipeline.TransformAsync(mockEvent, CancellationToken.None);

            // Assert
            Assert.Same(mockEvent, result);
        }

        [Fact]
        public async Task Should_throw_EventFilteredException_when_filter_returns_false()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);
            builder.AddFilter(e => false); // Filter always rejects

            var mockEvent = CreateMockEvent();

            var pipeline = builder.Build();

            // Act & Assert
            await Assert.ThrowsAsync<EventFilteredException>(
                () => pipeline.TransformAsync(mockEvent, CancellationToken.None));
        }

        [Fact]
        public async Task Should_evaluate_multiple_filters_in_sequence()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);
            builder.AddFilter(e => true);
            builder.AddFilter(e => true);
            builder.AddFilter(e => true);

            var mockEvent = CreateMockEvent();

            var pipeline = builder.Build();

            // Act
            var result = await pipeline.TransformAsync(mockEvent, CancellationToken.None);

            // Assert
            Assert.Same(mockEvent, result);
        }

        [Fact]
        public async Task Should_fail_if_any_filter_returns_false()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);
            builder.AddFilter(e => true);
            builder.AddFilter(e => false); // Second filter fails
            builder.AddFilter(e => true);

            var mockEvent = CreateMockEvent();

            var pipeline = builder.Build();

            // Act & Assert
            await Assert.ThrowsAsync<EventFilteredException>(
                () => pipeline.TransformAsync(mockEvent, CancellationToken.None));
        }

        [Fact]
        public async Task Should_include_event_info_in_exception_message()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);
            builder.AddFilter(e => false);

            var mockEvent = CreateMockEvent("SpecialEvent", 42);

            var pipeline = builder.Build();

            // Act
            var exception = await Assert.ThrowsAsync<EventFilteredException>(
                () => pipeline.TransformAsync(mockEvent, CancellationToken.None));

            // Assert
            Assert.Contains("42", exception.Message);
        }
    }

    public class FilterWithTransformersTests
    {
        [Fact]
        public async Task Should_apply_filter_before_transformers()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);

            var filterCalled = false;
            var transformerCalled = false;

            builder.AddFilter(e =>
            {
                filterCalled = true;
                return true;
            });

            var transformer = Substitute.For<IEventTransformer>();
            transformer.CanTransform(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
            transformer.TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    transformerCalled = true;
                    return Task.FromResult(callInfo.Arg<IEvent>());
                });

            builder.AddTransformer(transformer);

            var mockEvent = CreateMockEvent();

            var pipeline = builder.Build();

            // Act
            await pipeline.TransformAsync(mockEvent, CancellationToken.None);

            // Assert
            Assert.True(filterCalled);
            Assert.True(transformerCalled);
        }

        [Fact]
        public async Task Should_not_call_transformers_when_filter_rejects()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);

            builder.AddFilter(e => false); // Filter rejects

            var transformer = Substitute.For<IEventTransformer>();
            builder.AddTransformer(transformer);

            var mockEvent = CreateMockEvent();

            var pipeline = builder.Build();

            // Act
            try
            {
                await pipeline.TransformAsync(mockEvent, CancellationToken.None);
            }
            catch (EventFilteredException)
            {
                // Expected
            }

            // Assert
            await transformer.DidNotReceive().TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>());
        }
    }

    public class NoFiltersTests
    {
        [Fact]
        public async Task Should_work_without_filters()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);

            var mockEvent = CreateMockEvent();

            var pipeline = builder.Build();

            // Act
            var result = await pipeline.TransformAsync(mockEvent, CancellationToken.None);

            // Assert
            Assert.Same(mockEvent, result);
        }
    }

    public class BuildMethodTests
    {
        [Fact]
        public void Should_create_pipeline_with_filters_and_transformers()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);

            builder.AddFilter(e => true);

            var transformer = Substitute.For<IEventTransformer>();
            builder.AddTransformer(transformer);

            // Act
            var pipeline = builder.Build();

            // Assert
            Assert.NotNull(pipeline);
            // With filter and transformer, pipeline should have 2 transformers
            Assert.Equal(2, pipeline.Count);
        }

        [Fact]
        public void Should_create_pipeline_with_only_transformers()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);

            var transformer = Substitute.For<IEventTransformer>();
            builder.AddTransformer(transformer);

            // Act
            var pipeline = builder.Build();

            // Assert
            Assert.Equal(1, pipeline.Count);
        }

        [Fact]
        public void Should_create_empty_pipeline()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();
            var builder = new TransformationPipelineBuilder(loggerFactory);

            // Act
            var pipeline = builder.Build();

            // Assert
            Assert.Equal(0, pipeline.Count);
        }
    }
}
