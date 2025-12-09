using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Transformation;

public class CompositeTransformerTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_transformers_array_is_null()
        {
            // Arrange
            IEventTransformer[]? transformers = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CompositeTransformer(transformers!));
        }

        [Fact]
        public void Should_throw_ArgumentException_when_transformers_array_is_empty()
        {
            // Arrange
            var transformers = Array.Empty<IEventTransformer>();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new CompositeTransformer(transformers));
            Assert.Contains("At least one transformer is required", exception.Message);
        }

        [Fact]
        public void Should_create_instance_with_single_transformer()
        {
            // Arrange
            var transformer = Substitute.For<IEventTransformer>();

            // Act
            var sut = new CompositeTransformer(transformer);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_multiple_transformers()
        {
            // Arrange
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();
            var transformer3 = Substitute.For<IEventTransformer>();

            // Act
            var sut = new CompositeTransformer(transformer1, transformer2, transformer3);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class CanTransformMethod
    {
        [Fact]
        public void Should_return_true_when_any_transformer_can_transform()
        {
            // Arrange
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();
            transformer1.CanTransform("TestEvent", 1).Returns(false);
            transformer2.CanTransform("TestEvent", 1).Returns(true);
            var sut = new CompositeTransformer(transformer1, transformer2);

            // Act
            var result = sut.CanTransform("TestEvent", 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_when_no_transformer_can_transform()
        {
            // Arrange
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();
            transformer1.CanTransform("TestEvent", 1).Returns(false);
            transformer2.CanTransform("TestEvent", 1).Returns(false);
            var sut = new CompositeTransformer(transformer1, transformer2);

            // Act
            var result = sut.CanTransform("TestEvent", 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Should_check_all_transformers()
        {
            // Arrange
            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();
            transformer1.CanTransform(Arg.Any<string>(), Arg.Any<int>()).Returns(false);
            transformer2.CanTransform(Arg.Any<string>(), Arg.Any<int>()).Returns(false);
            var sut = new CompositeTransformer(transformer1, transformer2);

            // Act
            sut.CanTransform("TestEvent", 1);

            // Assert - both transformers should be checked (LINQ Any will stop at first true)
            transformer1.Received(1).CanTransform("TestEvent", 1);
        }
    }

    public class TransformAsyncMethod
    {
        [Fact]
        public async Task Should_apply_transformers_sequentially()
        {
            // Arrange
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

            var sut = new CompositeTransformer(transformer1, transformer2);

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(finalEvent, result);
        }

        [Fact]
        public async Task Should_skip_transformer_when_cannot_transform()
        {
            // Arrange
            var sourceEvent = Substitute.For<IEvent>();
            var transformedEvent = Substitute.For<IEvent>();

            sourceEvent.EventType.Returns("TestEvent");
            sourceEvent.EventVersion.Returns(1);

            var transformer1 = Substitute.For<IEventTransformer>();
            var transformer2 = Substitute.For<IEventTransformer>();

            transformer1.CanTransform("TestEvent", 1).Returns(false);
            transformer2.CanTransform("TestEvent", 1).Returns(true);
            transformer2.TransformAsync(sourceEvent, Arg.Any<CancellationToken>()).Returns(Task.FromResult(transformedEvent));

            var sut = new CompositeTransformer(transformer1, transformer2);

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(transformedEvent, result);
            await transformer1.DidNotReceive().TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_original_event_when_no_transformer_applies()
        {
            // Arrange
            var sourceEvent = Substitute.For<IEvent>();
            sourceEvent.EventType.Returns("TestEvent");
            sourceEvent.EventVersion.Returns(1);

            var transformer1 = Substitute.For<IEventTransformer>();
            transformer1.CanTransform("TestEvent", 1).Returns(false);

            var sut = new CompositeTransformer(transformer1);

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(sourceEvent, result);
        }

        [Fact]
        public async Task Should_pass_cancellation_token_to_transformers()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var sourceEvent = Substitute.For<IEvent>();
            sourceEvent.EventType.Returns("TestEvent");
            sourceEvent.EventVersion.Returns(1);

            var transformer = Substitute.For<IEventTransformer>();
            transformer.CanTransform("TestEvent", 1).Returns(true);
            transformer.TransformAsync(sourceEvent, cts.Token).Returns(Task.FromResult(sourceEvent));

            var sut = new CompositeTransformer(transformer);

            // Act
            await sut.TransformAsync(sourceEvent, cts.Token);

            // Assert
            await transformer.Received(1).TransformAsync(sourceEvent, cts.Token);
        }
    }
}
