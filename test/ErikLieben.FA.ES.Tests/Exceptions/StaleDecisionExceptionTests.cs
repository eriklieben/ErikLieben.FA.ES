using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.Validation;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class StaleDecisionExceptionTests
{
    [Fact]
    public void Should_create_from_validation_result()
    {
        // Arrange
        var validationResult = CheckpointValidationResult.VersionMismatch("stream-1", 5, 10);

        // Act
        var sut = new StaleDecisionException(validationResult);

        // Assert
        Assert.Equal(validationResult, sut.ValidationResult);
        Assert.NotNull(validationResult.Message);
        Assert.Contains(validationResult.Message, sut.Message);
    }

    [Fact]
    public void Should_create_from_version_details()
    {
        // Arrange
        var streamId = "stream-1";
        var expectedVersion = 5;
        var actualVersion = 10;

        // Act
        var sut = new StaleDecisionException(streamId, expectedVersion, actualVersion);

        // Assert
        Assert.Equal(streamId, sut.StreamId);
        Assert.Equal(expectedVersion, sut.ExpectedVersion);
        Assert.Equal(actualVersion, sut.ActualVersion);
    }

    [Fact]
    public void Should_include_error_code_in_message()
    {
        // Arrange
        var validationResult = CheckpointValidationResult.VersionMismatch("stream-1", 5, 10);

        // Act
        var sut = new StaleDecisionException(validationResult);

        // Assert
        Assert.Contains(StaleDecisionException.StaleDecisionErrorCode, sut.Message);
    }

    [Fact]
    public void Should_have_correct_error_code_constant()
    {
        // Assert
        Assert.Equal("ELFAES-STALE-0001", StaleDecisionException.StaleDecisionErrorCode);
    }

    [Fact]
    public void Should_inherit_from_EsException()
    {
        // Arrange
        var validationResult = CheckpointValidationResult.VersionMismatch("stream-1", 5, 10);

        // Act
        var sut = new StaleDecisionException(validationResult);

        // Assert
        Assert.IsType<EsException>(sut, exactMatch: false);
    }

    [Fact]
    public void Should_expose_stream_id_from_validation_result()
    {
        // Arrange
        var streamId = "my-stream";
        var validationResult = CheckpointValidationResult.VersionMismatch(streamId, 1, 2);

        // Act
        var sut = new StaleDecisionException(validationResult);

        // Assert
        Assert.Equal(streamId, sut.StreamId);
    }

    [Fact]
    public void Should_expose_expected_version_from_validation_result()
    {
        // Arrange
        var validationResult = CheckpointValidationResult.VersionMismatch("stream", 7, 10);

        // Act
        var sut = new StaleDecisionException(validationResult);

        // Assert
        Assert.Equal(7, sut.ExpectedVersion);
    }

    [Fact]
    public void Should_expose_actual_version_from_validation_result()
    {
        // Arrange
        var validationResult = CheckpointValidationResult.VersionMismatch("stream", 7, 12);

        // Act
        var sut = new StaleDecisionException(validationResult);

        // Assert
        Assert.Equal(12, sut.ActualVersion);
    }

    [Fact]
    public void Should_use_default_message_when_validation_result_message_is_null()
    {
        // Arrange
        var validationResult = new CheckpointValidationResult(false, "stream", 1, 2, null);

        // Act
        var sut = new StaleDecisionException(validationResult);

        // Assert
        Assert.Contains("Decision is based on stale data", sut.Message);
    }

    [Fact]
    public void Version_details_constructor_Should_create_validation_result()
    {
        // Arrange
        var streamId = "stream-1";
        var expectedVersion = 3;
        var actualVersion = 7;

        // Act
        var sut = new StaleDecisionException(streamId, expectedVersion, actualVersion);

        // Assert
        Assert.NotNull(sut.ValidationResult);
        Assert.False(sut.ValidationResult.IsValid);
        Assert.Equal(streamId, sut.ValidationResult.StreamId);
        Assert.Equal(expectedVersion, sut.ValidationResult.ExpectedVersion);
        Assert.Equal(actualVersion, sut.ValidationResult.ActualVersion);
    }

    [Fact]
    public void Version_details_constructor_Should_include_meaningful_message()
    {
        // Arrange
        var streamId = "order-123";
        var expectedVersion = 5;
        var actualVersion = 8;

        // Act
        var sut = new StaleDecisionException(streamId, expectedVersion, actualVersion);

        // Assert
        Assert.Contains("order-123", sut.Message);
        Assert.Contains("v5", sut.Message);
        Assert.Contains("v8", sut.Message);
        Assert.Contains("refresh", sut.Message.ToLowerInvariant());
    }
}
