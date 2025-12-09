using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Transformation;

public class TransformationPipelineTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_is_null()
        {
            // Arrange
            ILogger<TransformationPipeline>? logger = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TransformationPipeline(logger!));
        }

        [Fact]
        public void Should_create_instance_with_valid_logger()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();

            // Act
            var sut = new TransformationPipeline(logger);

            // Assert
            Assert.NotNull(sut);
            Assert.Equal(0, sut.Count);
        }
    }

    public class CountProperty
    {
        [Fact]
        public void Should_return_zero_when_no_transformers_added()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);

            // Act
            var count = sut.Count;

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void Should_return_correct_count_after_adding_transformers()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();

            // Act
            sut.AddTransformer(transformer1);
            sut.AddTransformer(transformer2);

            // Assert
            Assert.Equal(2, sut.Count);
        }
    }

    public class AddTransformerMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_transformer_is_null()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.AddTransformer(null!));
        }

        [Fact]
        public void Should_add_transformer_to_pipeline()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);
            var transformer = Substitute.For<IEventTransformer>();

            // Act
            sut.AddTransformer(transformer);

            // Assert
            Assert.Equal(1, sut.Count);
        }

        [Fact]
        public void Should_allow_adding_multiple_transformers()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();
            var transformer3 = Substitute.For<IEventTransformer>();

            // Act
            sut.AddTransformer(transformer1);
            sut.AddTransformer(transformer2);
            sut.AddTransformer(transformer3);

            // Assert
            Assert.Equal(3, sut.Count);
        }
    }

    public class CanTransformMethod
    {
        [Fact]
        public void Should_return_false_when_no_transformers()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);

            // Act
            var result = sut.CanTransform("TestEvent", 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Should_return_true_when_any_transformer_can_transform()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();
            transformer1.CanTransform("TestEvent", 1).Returns(false);
            transformer2.CanTransform("TestEvent", 1).Returns(true);
            sut.AddTransformer(transformer1);
            sut.AddTransformer(transformer2);

            // Act
            var result = sut.CanTransform("TestEvent", 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_when_no_transformer_can_transform()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);
            var transformer = Substitute.For<IEventTransformer>();
            transformer.CanTransform("TestEvent", 1).Returns(false);
            sut.AddTransformer(transformer);

            // Act
            var result = sut.CanTransform("TestEvent", 1);

            // Assert
            Assert.False(result);
        }
    }

    public class TransformAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_source_event_is_null()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.TransformAsync(null!));
        }

        [Fact]
        public async Task Should_return_source_event_when_no_transformers()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);
            var sourceEvent = Substitute.For<IEvent>();

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(sourceEvent, result);
        }

        [Fact]
        public async Task Should_apply_transformers_sequentially()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);

            var sourceEvent = Substitute.For<IEvent>();
            var intermediateEvent = Substitute.For<IEvent>();
            var finalEvent = Substitute.For<IEvent>();

            sourceEvent.EventType.Returns("TestEvent");
            sourceEvent.EventVersion.Returns(1);
            intermediateEvent.EventType.Returns("TestEvent");
            intermediateEvent.EventVersion.Returns(2);

            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();

            transformer1.CanTransform("TestEvent", 1).Returns(true);
            transformer1.TransformAsync(sourceEvent, Arg.Any<CancellationToken>()).Returns(Task.FromResult(intermediateEvent));

            transformer2.CanTransform("TestEvent", 2).Returns(true);
            transformer2.TransformAsync(intermediateEvent, Arg.Any<CancellationToken>()).Returns(Task.FromResult(finalEvent));

            sut.AddTransformer(transformer1);
            sut.AddTransformer(transformer2);

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(finalEvent, result);
        }

        [Fact]
        public async Task Should_skip_transformer_when_cannot_transform()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);

            var sourceEvent = Substitute.For<IEvent>();
            var transformedEvent = Substitute.For<IEvent>();

            sourceEvent.EventType.Returns("TestEvent");
            sourceEvent.EventVersion.Returns(1);

            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();

            transformer1.CanTransform("TestEvent", 1).Returns(false);
            transformer2.CanTransform("TestEvent", 1).Returns(true);
            transformer2.TransformAsync(sourceEvent, Arg.Any<CancellationToken>()).Returns(Task.FromResult(transformedEvent));

            sut.AddTransformer(transformer1);
            sut.AddTransformer(transformer2);

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(transformedEvent, result);
            await transformer1.DidNotReceive().TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_pass_cancellation_token_to_transformers()
        {
            // Arrange
            var logger = Substitute.For<ILogger<TransformationPipeline>>();
            var sut = new TransformationPipeline(logger);
            var cts = new CancellationTokenSource();

            var sourceEvent = Substitute.For<IEvent>();
            sourceEvent.EventType.Returns("TestEvent");
            sourceEvent.EventVersion.Returns(1);

            var transformer = Substitute.For<IEventTransformer>();
            transformer.CanTransform("TestEvent", 1).Returns(true);
            transformer.TransformAsync(sourceEvent, cts.Token).Returns(Task.FromResult(sourceEvent));

            sut.AddTransformer(transformer);

            // Act
            await sut.TransformAsync(sourceEvent, cts.Token);

            // Assert
            await transformer.Received(1).TransformAsync(sourceEvent, cts.Token);
        }
    }
}
