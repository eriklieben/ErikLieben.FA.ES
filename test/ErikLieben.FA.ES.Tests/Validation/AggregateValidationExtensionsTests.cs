using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Validation;
using ErikLieben.FA.ES.VersionTokenParts;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Validation;

public class AggregateValidationExtensionsTests
{
    [Fact]
    public void ValidateCheckpoint_Should_throw_when_aggregate_is_null()
    {
        // Arrange
        Aggregate? aggregate = null;
        var context = DecisionContext.Empty;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => aggregate!.ValidateCheckpoint(context));
    }

    [Fact]
    public void ValidateCheckpoint_Should_return_NoCheckpointProvided_when_context_is_null()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var aggregate = new TestAggregate(stream);

        // Act
        var result = aggregate.ValidateCheckpoint(null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("No checkpoint provided", result.Message);
    }

    [Fact]
    public void ValidateCheckpoint_Should_return_NoCheckpointProvided_when_context_is_empty()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var aggregate = new TestAggregate(stream);

        // Act
        var result = aggregate.ValidateCheckpoint(DecisionContext.Empty);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("No checkpoint provided", result.Message);
    }

    [Fact]
    public void ValidateCheckpoint_Should_return_Valid_when_version_matches()
    {
        // Arrange
        var streamId = "order-stream";
        var stream = Substitute.For<IEventStream>();
        stream.StreamIdentifier.Returns(streamId);
        stream.CurrentVersion.Returns(5);

        var aggregate = new TestAggregate(stream);

        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier(streamId, 5)
        };
        var context = new DecisionContext("fingerprint", checkpoint, DateTimeOffset.UtcNow);

        // Act
        var result = aggregate.ValidateCheckpoint(context);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateCheckpoint_Should_return_VersionMismatch_when_aggregate_changed()
    {
        // Arrange
        var streamId = "order-stream";
        var stream = Substitute.For<IEventStream>();
        stream.StreamIdentifier.Returns(streamId);
        stream.CurrentVersion.Returns(10);

        var aggregate = new TestAggregate(stream);

        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier(streamId, 5)
        };
        var context = new DecisionContext("fingerprint", checkpoint, DateTimeOffset.UtcNow);

        // Act
        var result = aggregate.ValidateCheckpoint(context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(streamId, result.StreamId);
        Assert.Equal(5, result.ExpectedVersion);
        Assert.Equal(10, result.ActualVersion);
    }

    [Fact]
    public void ValidateCheckpoint_Should_return_Valid_when_only_fingerprint_provided()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var aggregate = new TestAggregate(stream);
        var context = DecisionContext.FromFingerprint("some-fingerprint");

        // Act
        var result = aggregate.ValidateCheckpoint(context);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateCheckpoint_Should_return_Valid_when_stream_not_in_checkpoint()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        stream.StreamIdentifier.Returns("different-stream");
        stream.CurrentVersion.Returns(10);

        var aggregate = new TestAggregate(stream);

        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier("original-stream", 5)
        };
        var context = new DecisionContext("fingerprint", checkpoint, DateTimeOffset.UtcNow);

        // Act
        var result = aggregate.ValidateCheckpoint(context);

        // Assert
        Assert.True(result.IsValid);
    }

    private class TestAggregate : Aggregate
    {
        public TestAggregate(IEventStream stream) : base(stream)
        {
        }
    }
}
