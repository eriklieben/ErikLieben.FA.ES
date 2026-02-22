using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.Validation;
using ErikLieben.FA.ES.VersionTokenParts;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Validation;

public class DecisionContextTests
{
    [Fact]
    public void Empty_Should_have_no_fingerprint_or_stream_versions()
    {
        // Act
        var context = DecisionContext.Empty;

        // Assert
        Assert.Null(context.CheckpointFingerprint);
        Assert.Null(context.StreamVersions);
        Assert.Equal(DateTimeOffset.MinValue, context.DecisionTimestamp);
    }

    [Fact]
    public void IsEmpty_Should_return_true_for_empty_context()
    {
        // Act
        var context = DecisionContext.Empty;

        // Assert
        Assert.True(context.IsEmpty);
    }

    [Fact]
    public void IsEmpty_Should_return_true_when_fingerprint_and_versions_are_null()
    {
        // Arrange
        var context = new DecisionContext(null, null, DateTimeOffset.UtcNow);

        // Assert
        Assert.True(context.IsEmpty);
    }

    [Fact]
    public void IsEmpty_Should_return_true_when_versions_empty_and_fingerprint_null()
    {
        // Arrange
        var context = new DecisionContext(null, new Checkpoint(), DateTimeOffset.UtcNow);

        // Assert
        Assert.True(context.IsEmpty);
    }

    [Fact]
    public void IsEmpty_Should_return_false_when_fingerprint_provided()
    {
        // Arrange
        var context = new DecisionContext("fingerprint", null, DateTimeOffset.UtcNow);

        // Assert
        Assert.False(context.IsEmpty);
    }

    [Fact]
    public void IsEmpty_Should_return_false_when_stream_versions_provided()
    {
        // Arrange
        var checkpoint = new Checkpoint
        {
            [new ObjectIdentifier("order", "123")] = new VersionIdentifier("stream-1", 5)
        };
        var context = new DecisionContext(null, checkpoint, DateTimeOffset.UtcNow);

        // Assert
        Assert.False(context.IsEmpty);
    }

    [Fact]
    public void FromFingerprint_Should_create_context_with_fingerprint()
    {
        // Arrange
        var fingerprint = "test-fingerprint-hash";

        // Act
        var context = DecisionContext.FromFingerprint(fingerprint);

        // Assert
        Assert.Equal(fingerprint, context.CheckpointFingerprint);
        Assert.Null(context.StreamVersions);
        Assert.False(context.IsEmpty);
        Assert.True(context.DecisionTimestamp > DateTimeOffset.MinValue);
    }

    [Fact]
    public void FromHeader_Should_return_empty_when_header_is_null()
    {
        // Act
        var context = DecisionContext.FromHeader(null);

        // Assert
        Assert.Same(DecisionContext.Empty, context);
    }

    [Fact]
    public void FromHeader_Should_create_context_from_header_value()
    {
        // Arrange
        var headerValue = "header-fingerprint-value";

        // Act
        var context = DecisionContext.FromHeader(headerValue);

        // Assert
        Assert.Equal(headerValue, context.CheckpointFingerprint);
        Assert.False(context.IsEmpty);
    }

    [Fact]
    public void DecisionContext_Should_be_record_with_value_equality()
    {
        // Arrange
        var fingerprint = "test-fingerprint";
        var timestamp = DateTimeOffset.UtcNow;
        var context1 = new DecisionContext(fingerprint, null, timestamp);
        var context2 = new DecisionContext(fingerprint, null, timestamp);

        // Assert
        Assert.Equal(context1, context2);
    }

    [Fact]
    public void DecisionContext_Should_allow_with_expression()
    {
        // Arrange
        var original = new DecisionContext("fingerprint", null, DateTimeOffset.UtcNow);

        // Act
        var modified = original with { CheckpointFingerprint = "new-fingerprint" };

        // Assert
        Assert.NotEqual(original.CheckpointFingerprint, modified.CheckpointFingerprint);
        Assert.Equal("new-fingerprint", modified.CheckpointFingerprint);
        Assert.Equal(original.DecisionTimestamp, modified.DecisionTimestamp);
    }
}
