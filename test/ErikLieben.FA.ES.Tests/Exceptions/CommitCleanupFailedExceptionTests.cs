using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.Tests.Exceptions;

/// <summary>
/// Tests for the CommitCleanupFailedException class.
/// </summary>
public class CommitCleanupFailedExceptionTests
{
    [Fact]
    public void Constructor_should_set_all_properties()
    {
        // Arrange
        var streamId = "test-stream-123";
        var originalVersion = 5;
        var attemptedVersion = 10;
        var cleanupFromVersion = 6;
        var cleanupToVersion = 10;
        var cleanupException = new InvalidOperationException("Cleanup failed");
        var originalException = new TimeoutException("Original timeout");

        // Act
        var exception = new CommitCleanupFailedException(
            streamId,
            originalVersion,
            attemptedVersion,
            cleanupFromVersion,
            cleanupToVersion,
            cleanupException,
            originalException);

        // Assert
        Assert.Equal(streamId, exception.StreamIdentifier);
        Assert.Equal(originalVersion, exception.OriginalVersion);
        Assert.Equal(attemptedVersion, exception.AttemptedVersion);
        Assert.Equal(cleanupFromVersion, exception.CleanupFromVersion);
        Assert.Equal(cleanupToVersion, exception.CleanupToVersion);
        Assert.Same(cleanupException, exception.CleanupException);
        Assert.Same(originalException, exception.OriginalCommitException);
        Assert.Same(originalException, exception.InnerException);
    }

    [Fact]
    public void Constructor_should_format_message_with_all_details()
    {
        // Arrange
        var streamId = "order-42";
        var cleanupFromVersion = 3;
        var cleanupToVersion = 7;
        var cleanupException = new Exception("Storage unavailable");
        var originalException = new Exception("Connection reset");

        // Act
        var exception = new CommitCleanupFailedException(
            streamId, 2, 7, cleanupFromVersion, cleanupToVersion,
            cleanupException, originalException);

        // Assert
        Assert.Contains(streamId, exception.Message);
        Assert.Contains("broken state", exception.Message);
        Assert.Contains($"{cleanupFromVersion}-{cleanupToVersion}", exception.Message);
        Assert.Contains("Connection reset", exception.Message);
        Assert.Contains("Storage unavailable", exception.Message);
    }

    [Fact]
    public void Exception_should_have_correct_error_code()
    {
        // Arrange & Act
        var exception = new CommitCleanupFailedException(
            "stream", 0, 1, 1, 1,
            new Exception("cleanup"), new Exception("original"));

        // Assert
        Assert.Contains("ELFAES-COMMIT-0002", exception.Message);
    }

    [Fact]
    public void Exception_should_handle_null_streamIdentifier()
    {
        // Arrange & Act
        var exception = new CommitCleanupFailedException(
            null, 0, 1, 1, 1,
            new Exception("cleanup"), new Exception("original"));

        // Assert
        Assert.Null(exception.StreamIdentifier);
        Assert.Contains("''", exception.Message); // Empty quotes for null stream
    }
}
