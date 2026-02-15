using ErikLieben.FA.ES.Validation;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Validation;

public class CheckpointValidationResultTests
{
    [Fact]
    public void Valid_Should_return_valid_result()
    {
        // Act
        var result = CheckpointValidationResult.Valid();

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.StreamId);
        Assert.Null(result.ExpectedVersion);
        Assert.Null(result.ActualVersion);
        Assert.Null(result.Message);
    }

    [Fact]
    public void NoCheckpointProvided_Should_return_valid_result_with_message()
    {
        // Act
        var result = CheckpointValidationResult.NoCheckpointProvided();

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.StreamId);
        Assert.Null(result.ExpectedVersion);
        Assert.Null(result.ActualVersion);
        Assert.Equal("No checkpoint provided", result.Message);
    }

    [Fact]
    public void VersionMismatch_Should_return_invalid_result_with_details()
    {
        // Arrange
        var streamId = "test-stream";
        var expectedVersion = 5;
        var actualVersion = 7;

        // Act
        var result = CheckpointValidationResult.VersionMismatch(streamId, expectedVersion, actualVersion);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(streamId, result.StreamId);
        Assert.Equal(expectedVersion, result.ExpectedVersion);
        Assert.Equal(actualVersion, result.ActualVersion);
        Assert.Contains(streamId, result.Message);
        Assert.Contains("5", result.Message);
        Assert.Contains("7", result.Message);
    }

    [Fact]
    public void Should_be_record_with_value_equality()
    {
        // Arrange
        var result1 = CheckpointValidationResult.Valid();
        var result2 = CheckpointValidationResult.Valid();

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void VersionMismatch_results_with_same_values_Should_be_equal()
    {
        // Arrange
        var result1 = CheckpointValidationResult.VersionMismatch("stream1", 1, 2);
        var result2 = CheckpointValidationResult.VersionMismatch("stream1", 1, 2);

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void VersionMismatch_results_with_different_values_Should_not_be_equal()
    {
        // Arrange
        var result1 = CheckpointValidationResult.VersionMismatch("stream1", 1, 2);
        var result2 = CheckpointValidationResult.VersionMismatch("stream2", 1, 2);

        // Assert
        Assert.NotEqual(result1, result2);
    }
}
