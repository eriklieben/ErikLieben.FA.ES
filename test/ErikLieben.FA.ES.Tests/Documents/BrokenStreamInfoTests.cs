using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Tests.Documents;

/// <summary>
/// Tests for the BrokenStreamInfo class.
/// </summary>
public class BrokenStreamInfoTests
{
    [Fact]
    public void BrokenStreamInfo_should_have_default_values()
    {
        // Act
        var info = new BrokenStreamInfo();

        // Assert
        Assert.Equal(default, info.BrokenAt);
        Assert.Equal(0, info.OrphanedFromVersion);
        Assert.Equal(0, info.OrphanedToVersion);
        Assert.Null(info.ErrorMessage);
        Assert.Null(info.OriginalExceptionType);
        Assert.Null(info.CleanupExceptionType);
    }

    [Fact]
    public void BrokenStreamInfo_should_store_all_properties()
    {
        // Arrange
        var brokenAt = DateTimeOffset.UtcNow;
        var fromVersion = 5;
        var toVersion = 10;
        var errorMessage = "Connection timeout during cleanup";
        var originalExceptionType = "System.TimeoutException";
        var cleanupExceptionType = "Azure.RequestFailedException";

        // Act
        var info = new BrokenStreamInfo
        {
            BrokenAt = brokenAt,
            OrphanedFromVersion = fromVersion,
            OrphanedToVersion = toVersion,
            ErrorMessage = errorMessage,
            OriginalExceptionType = originalExceptionType,
            CleanupExceptionType = cleanupExceptionType
        };

        // Assert
        Assert.Equal(brokenAt, info.BrokenAt);
        Assert.Equal(fromVersion, info.OrphanedFromVersion);
        Assert.Equal(toVersion, info.OrphanedToVersion);
        Assert.Equal(errorMessage, info.ErrorMessage);
        Assert.Equal(originalExceptionType, info.OriginalExceptionType);
        Assert.Equal(cleanupExceptionType, info.CleanupExceptionType);
    }

    [Fact]
    public void BrokenStreamInfo_should_handle_single_version_range()
    {
        // Arrange & Act
        var info = new BrokenStreamInfo
        {
            OrphanedFromVersion = 7,
            OrphanedToVersion = 7
        };

        // Assert
        Assert.Equal(7, info.OrphanedFromVersion);
        Assert.Equal(7, info.OrphanedToVersion);
    }
}
