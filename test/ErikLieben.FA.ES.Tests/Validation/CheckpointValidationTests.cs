using ErikLieben.FA.ES.Validation;
using ErikLieben.FA.ES.VersionTokenParts;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Validation;

public class CheckpointValidationTests
{
    [Fact]
    public void Validate_Should_throw_when_stream_is_null()
    {
        // Arrange
        IEventStream? stream = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CheckpointValidation.Validate(stream!));
    }

    [Fact]
    public void Validate_Should_return_NoCheckpointProvided_when_no_checkpoint_or_fingerprint()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();

        // Act
        var result = CheckpointValidation.Validate(stream);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("No checkpoint provided", result.Message);
    }

    [Fact]
    public void Validate_Should_return_Valid_when_only_fingerprint_provided()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var fingerprint = "abc123fingerprint";

        // Act
        var result = CheckpointValidation.Validate(stream, checkpointFingerprint: fingerprint);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Validate_Should_return_Valid_when_stream_not_in_checkpoint()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        stream.StreamIdentifier.Returns("stream-not-in-checkpoint");
        stream.CurrentVersion.Returns(5);

        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier("different-stream", 3)
        };

        // Act
        var result = CheckpointValidation.Validate(stream, checkpoint: checkpoint);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Should_return_Valid_when_version_matches()
    {
        // Arrange
        var streamId = "order-stream";
        var stream = Substitute.For<IEventStream>();
        stream.StreamIdentifier.Returns(streamId);
        stream.CurrentVersion.Returns(5);

        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier(streamId, 5)
        };

        // Act
        var result = CheckpointValidation.Validate(stream, checkpoint: checkpoint);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Should_return_VersionMismatch_when_stream_version_differs()
    {
        // Arrange
        var streamId = "order-stream";
        var stream = Substitute.For<IEventStream>();
        stream.StreamIdentifier.Returns(streamId);
        stream.CurrentVersion.Returns(7);

        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier(streamId, 5)
        };

        // Act
        var result = CheckpointValidation.Validate(stream, checkpoint: checkpoint);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(streamId, result.StreamId);
        Assert.Equal(5, result.ExpectedVersion);
        Assert.Equal(7, result.ActualVersion);
    }

    [Fact]
    public void Validate_Should_return_NoCheckpointProvided_when_checkpoint_is_empty()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var checkpoint = new Checkpoint(); // Empty checkpoint

        // Act
        var result = CheckpointValidation.Validate(stream, checkpoint: checkpoint);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("No checkpoint provided", result.Message);
    }

    [Fact]
    public void Validate_Should_validate_against_checkpoint_even_with_fingerprint()
    {
        // Arrange
        var streamId = "order-stream";
        var stream = Substitute.For<IEventStream>();
        stream.StreamIdentifier.Returns(streamId);
        stream.CurrentVersion.Returns(10);

        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier(streamId, 5)
        };

        // Act
        var result = CheckpointValidation.Validate(stream, "fingerprint", checkpoint);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(5, result.ExpectedVersion);
        Assert.Equal(10, result.ActualVersion);
    }

    [Fact]
    public void Validate_Should_match_first_matching_stream_in_checkpoint()
    {
        // Arrange
        var targetStreamId = "target-stream";
        var stream = Substitute.For<IEventStream>();
        stream.StreamIdentifier.Returns(targetStreamId);
        stream.CurrentVersion.Returns(5);

        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier("other-stream", 3),
            [new ObjectIdentifier("order", "456")] = new VersionIdentifier(targetStreamId, 5),
            [new ObjectIdentifier("order", "789")] = new VersionIdentifier("another-stream", 7)
        };

        // Act
        var result = CheckpointValidation.Validate(stream, checkpoint: checkpoint);

        // Assert
        Assert.True(result.IsValid);
    }
}
