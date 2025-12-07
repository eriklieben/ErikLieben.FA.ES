using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Transformation;

public class FunctionTransformerTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_async_transform_function_is_null()
        {
            // Arrange
            Func<IEvent, CancellationToken, Task<IEvent>>? transformFunc = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FunctionTransformer(transformFunc!));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_sync_transform_function_is_null()
        {
            // Arrange
            Func<IEvent, IEvent>? transformFunc = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FunctionTransformer(transformFunc!));
        }

        [Fact]
        public void Should_create_instance_with_valid_async_transform_function()
        {
            // Arrange
            Func<IEvent, CancellationToken, Task<IEvent>> transformFunc = (e, _) => Task.FromResult(e);

            // Act
            var sut = new FunctionTransformer(transformFunc);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_valid_sync_transform_function()
        {
            // Arrange
            Func<IEvent, IEvent> transformFunc = e => e;

            // Act
            var sut = new FunctionTransformer(transformFunc);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class CanTransformMethod
    {
        [Fact]
        public void Should_return_true_by_default_when_no_predicate_provided()
        {
            // Arrange
            Func<IEvent, IEvent> transformFunc = e => e;
            var sut = new FunctionTransformer(transformFunc);

            // Act
            var result = sut.CanTransform("AnyEvent", 1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Should_use_custom_predicate_when_provided()
        {
            // Arrange
            Func<IEvent, IEvent> transformFunc = e => e;
            Func<string, int, bool> canTransformFunc = (name, version) => name == "TargetEvent" && version == 1;
            var sut = new FunctionTransformer(transformFunc, canTransformFunc);

            // Act & Assert
            Assert.True(sut.CanTransform("TargetEvent", 1));
            Assert.False(sut.CanTransform("OtherEvent", 1));
            Assert.False(sut.CanTransform("TargetEvent", 2));
        }

        [Theory]
        [InlineData("EventA", 1, true)]
        [InlineData("EventB", 2, false)]
        [InlineData("", 0, true)]
        public void Should_evaluate_predicate_correctly_with_various_inputs(string eventName, int version, bool expected)
        {
            // Arrange
            Func<IEvent, IEvent> transformFunc = e => e;
            Func<string, int, bool> canTransformFunc = (name, ver) => name == "EventA" || ver == 0;
            var sut = new FunctionTransformer(transformFunc, canTransformFunc);

            // Act
            var result = sut.CanTransform(eventName, version);

            // Assert
            Assert.Equal(expected, result);
        }
    }

    public class TransformAsyncMethod
    {
        [Fact]
        public async Task Should_apply_async_transformation_function()
        {
            // Arrange
            var sourceEvent = Substitute.For<IEvent>();
            var transformedEvent = Substitute.For<IEvent>();
            Func<IEvent, CancellationToken, Task<IEvent>> transformFunc = (e, _) => Task.FromResult(transformedEvent);
            var sut = new FunctionTransformer(transformFunc);

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(transformedEvent, result);
        }

        [Fact]
        public async Task Should_apply_sync_transformation_function()
        {
            // Arrange
            var sourceEvent = Substitute.For<IEvent>();
            var transformedEvent = Substitute.For<IEvent>();
            Func<IEvent, IEvent> transformFunc = e => transformedEvent;
            var sut = new FunctionTransformer(transformFunc);

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(transformedEvent, result);
        }

        [Fact]
        public async Task Should_pass_cancellation_token_to_async_function()
        {
            // Arrange
            var tokenReceived = false;
            var cts = new CancellationTokenSource();
            var sourceEvent = Substitute.For<IEvent>();
            Func<IEvent, CancellationToken, Task<IEvent>> transformFunc = (e, ct) =>
            {
                tokenReceived = ct == cts.Token;
                return Task.FromResult(e);
            };
            var sut = new FunctionTransformer(transformFunc);

            // Act
            await sut.TransformAsync(sourceEvent, cts.Token);

            // Assert
            Assert.True(tokenReceived);
        }

        [Fact]
        public async Task Should_return_source_event_when_identity_function()
        {
            // Arrange
            var sourceEvent = Substitute.For<IEvent>();
            Func<IEvent, IEvent> transformFunc = e => e;
            var sut = new FunctionTransformer(transformFunc);

            // Act
            var result = await sut.TransformAsync(sourceEvent);

            // Assert
            Assert.Same(sourceEvent, result);
        }
    }
}
