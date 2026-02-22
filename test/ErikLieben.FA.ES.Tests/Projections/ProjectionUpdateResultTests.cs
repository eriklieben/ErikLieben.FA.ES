using ErikLieben.FA.ES.Projections;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Projections;

public class ProjectionUpdateResultTests
{
    [Fact]
    public void Success_should_return_not_skipped_result()
    {
        // Arrange & Act
        var result = ProjectionUpdateResult.Success;

        // Assert
        Assert.False(result.Skipped);
        Assert.Equal(ProjectionStatus.Active, result.Status);
        Assert.Null(result.SkippedToken);
    }

    [Fact]
    public void SkippedDueToStatus_should_return_skipped_result_with_token()
    {
        // Arrange
        var token = new VersionToken("object__id__stream__00000000000000000001");

        // Act
        var result = ProjectionUpdateResult.SkippedDueToStatus(ProjectionStatus.Rebuilding, token);

        // Assert
        Assert.True(result.Skipped);
        Assert.Equal(ProjectionStatus.Rebuilding, result.Status);
        Assert.Same(token, result.SkippedToken);
    }

    [Theory]
    [InlineData(ProjectionStatus.Rebuilding)]
    [InlineData(ProjectionStatus.Disabled)]
    public void SkippedDueToStatus_should_preserve_status(ProjectionStatus status)
    {
        // Arrange
        var token = new VersionToken("object__id__stream__00000000000000000001");

        // Act
        var result = ProjectionUpdateResult.SkippedDueToStatus(status, token);

        // Assert
        Assert.Equal(status, result.Status);
    }

    [Fact]
    public void Success_is_a_singleton()
    {
        // Arrange & Act
        var result1 = ProjectionUpdateResult.Success;
        var result2 = ProjectionUpdateResult.Success;

        // Assert - Both should reference the same instance
        Assert.Same(result1, result2);
    }

    [Fact]
    public void Record_equality_should_work_for_success_results()
    {
        // Arrange
        var result1 = new ProjectionUpdateResult { Skipped = false, Status = ProjectionStatus.Active };
        var result2 = new ProjectionUpdateResult { Skipped = false, Status = ProjectionStatus.Active };

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Record_equality_should_work_for_skipped_results()
    {
        // Arrange
        var token = new VersionToken("object__id__stream__00000000000000000001");
        var result1 = ProjectionUpdateResult.SkippedDueToStatus(ProjectionStatus.Rebuilding, token);
        var result2 = ProjectionUpdateResult.SkippedDueToStatus(ProjectionStatus.Rebuilding, token);

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Record_inequality_should_detect_different_statuses()
    {
        // Arrange
        var token = new VersionToken("object__id__stream__00000000000000000001");
        var result1 = ProjectionUpdateResult.SkippedDueToStatus(ProjectionStatus.Rebuilding, token);
        var result2 = ProjectionUpdateResult.SkippedDueToStatus(ProjectionStatus.Disabled, token);

        // Assert
        Assert.NotEqual(result1, result2);
    }
}
