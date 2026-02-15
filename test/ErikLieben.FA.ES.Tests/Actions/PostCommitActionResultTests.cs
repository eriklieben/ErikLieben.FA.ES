using ErikLieben.FA.ES.Actions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Actions;

public class PostCommitActionResultTests
{
    [Fact]
    public void Succeeded_Should_create_success_result()
    {
        // Arrange
        var name = "TestAction";
        var type = typeof(TestAction);
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var result = PostCommitActionResult.Succeeded(name, type, duration);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(name, result.ActionName);
        Assert.Equal(type, result.ActionType);
        Assert.Equal(duration, result.Duration);
        Assert.IsType<SucceededPostCommitAction>(result);
    }

    [Fact]
    public void Failed_Should_create_failure_result()
    {
        // Arrange
        var name = "TestAction";
        var type = typeof(TestAction);
        var error = new InvalidOperationException("Test error");
        var attempts = 3;
        var duration = TimeSpan.FromMilliseconds(500);

        // Act
        var result = PostCommitActionResult.Failed(name, type, error, attempts, duration);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(name, result.ActionName);
        Assert.Equal(type, result.ActionType);
        Assert.Equal(duration, result.Duration);
        Assert.IsType<FailedPostCommitAction>(result);
    }

    [Fact]
    public void FailedPostCommitAction_Should_contain_error_details()
    {
        // Arrange
        var name = "TestAction";
        var type = typeof(TestAction);
        var error = new InvalidOperationException("Test error");
        var attempts = 3;
        var duration = TimeSpan.FromMilliseconds(500);

        // Act
        var result = PostCommitActionResult.Failed(name, type, error, attempts, duration);
        var failedResult = (FailedPostCommitAction)result;

        // Assert
        Assert.Equal(error, failedResult.Error);
        Assert.Equal(attempts, failedResult.RetryAttempts);
        Assert.Equal(duration, failedResult.TotalDuration);
    }

    [Fact]
    public void SucceededPostCommitAction_Should_be_record()
    {
        // Arrange
        var result1 = new SucceededPostCommitAction("Test", typeof(TestAction), TimeSpan.FromMilliseconds(100));
        var result2 = new SucceededPostCommitAction("Test", typeof(TestAction), TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void FailedPostCommitAction_Should_be_record()
    {
        // Arrange
        var error = new Exception("Test");
        var result1 = new FailedPostCommitAction("Test", typeof(TestAction), error, 1, TimeSpan.FromMilliseconds(100));
        var result2 = new FailedPostCommitAction("Test", typeof(TestAction), error, 1, TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Equal(result1, result2);
    }

    private class TestAction { }
}
