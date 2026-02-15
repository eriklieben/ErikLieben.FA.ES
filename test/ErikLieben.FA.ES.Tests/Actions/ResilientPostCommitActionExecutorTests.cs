using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Actions;

public class ResilientPostCommitActionExecutorTests
{
    [Fact]
    public void Constructor_Should_throw_when_options_is_null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ResilientPostCommitActionExecutor((PostCommitRetryOptions)null!));
    }

    [Fact]
    public void Constructor_Should_throw_when_pipeline_is_null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ResilientPostCommitActionExecutor((ResiliencePipeline)null!));
    }

    [Fact]
    public async Task ExecuteAsync_Should_throw_when_action_is_null()
    {
        // Arrange
        var executor = new ResilientPostCommitActionExecutor(PostCommitRetryOptions.Default);
        var events = new List<JsonEvent>();
        var document = Substitute.For<IObjectDocument>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            executor.ExecuteAsync(null!, events, document));
    }

    [Fact]
    public async Task ExecuteAsync_Should_return_success_when_action_succeeds()
    {
        // Arrange
        var executor = new ResilientPostCommitActionExecutor(PostCommitRetryOptions.NoRetry);
        var action = Substitute.For<IAsyncPostCommitAction>();
        var events = new List<JsonEvent> { new JsonEvent { EventType = "Test", EventVersion = 0 } };
        var document = Substitute.For<IObjectDocument>();

        action.PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), Arg.Any<IObjectDocument>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await executor.ExecuteAsync(action, events, document);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<SucceededPostCommitAction>(result);
        await action.Received(1).PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), document);
    }

    [Fact]
    public async Task ExecuteAsync_Should_return_failure_when_action_fails_after_retries()
    {
        // Arrange
        var options = new PostCommitRetryOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(1)
        };
        var executor = new ResilientPostCommitActionExecutor(options);
        var action = Substitute.For<IAsyncPostCommitAction>();
        var events = new List<JsonEvent> { new JsonEvent { EventType = "Test", EventVersion = 0 } };
        var document = Substitute.For<IObjectDocument>();
        var expectedException = new InvalidOperationException("Test failure");

        action.PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), Arg.Any<IObjectDocument>())
            .ThrowsAsync(expectedException);

        // Act
        var result = await executor.ExecuteAsync(action, events, document);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<FailedPostCommitAction>(result);
        Assert.Equal(expectedException, failedResult.Error);
        Assert.Equal(3, failedResult.RetryAttempts); // 1 initial + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_Should_not_retry_when_no_retry_configured()
    {
        // Arrange
        var executor = new ResilientPostCommitActionExecutor(PostCommitRetryOptions.NoRetry);
        var action = Substitute.For<IAsyncPostCommitAction>();
        var events = new List<JsonEvent> { new JsonEvent { EventType = "Test", EventVersion = 0 } };
        var document = Substitute.For<IObjectDocument>();

        action.PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), Arg.Any<IObjectDocument>())
            .ThrowsAsync(new InvalidOperationException("Test failure"));

        // Act
        var result = await executor.ExecuteAsync(action, events, document);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<FailedPostCommitAction>(result);
        Assert.Equal(1, failedResult.RetryAttempts);
        await action.Received(1).PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), document);
    }

    [Fact]
    public async Task ExecuteAllAsync_Should_execute_all_actions()
    {
        // Arrange
        var executor = new ResilientPostCommitActionExecutor(PostCommitRetryOptions.NoRetry);
        var action1 = Substitute.For<IAsyncPostCommitAction>();
        var action2 = Substitute.For<IAsyncPostCommitAction>();
        var events = new List<JsonEvent> { new JsonEvent { EventType = "Test", EventVersion = 0 } };
        var document = Substitute.For<IObjectDocument>();

        action1.PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), Arg.Any<IObjectDocument>())
            .Returns(Task.CompletedTask);
        action2.PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), Arg.Any<IObjectDocument>())
            .Returns(Task.CompletedTask);

        // Act
        var results = await executor.ExecuteAllAsync([action1, action2], events, document);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsSuccess));
    }

    [Fact]
    public async Task ExecuteAllAsync_Should_continue_after_failure()
    {
        // Arrange
        var executor = new ResilientPostCommitActionExecutor(PostCommitRetryOptions.NoRetry);
        var action1 = Substitute.For<IAsyncPostCommitAction>();
        var action2 = Substitute.For<IAsyncPostCommitAction>();
        var events = new List<JsonEvent> { new JsonEvent { EventType = "Test", EventVersion = 0 } };
        var document = Substitute.For<IObjectDocument>();

        action1.PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), Arg.Any<IObjectDocument>())
            .ThrowsAsync(new InvalidOperationException("Action 1 failed"));
        action2.PostCommitAsync(Arg.Any<IEnumerable<JsonEvent>>(), Arg.Any<IObjectDocument>())
            .Returns(Task.CompletedTask);

        // Act
        var results = await executor.ExecuteAllAsync([action1, action2], events, document);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsSuccess);
        Assert.True(results[1].IsSuccess);
    }

    [Fact]
    public void CreateDefault_Should_return_executor_with_default_options()
    {
        // Act
        var executor = ResilientPostCommitActionExecutor.CreateDefault();

        // Assert
        Assert.NotNull(executor);
    }

    [Fact]
    public async Task ExecuteAsync_Should_record_correct_action_name()
    {
        // Arrange
        var executor = new ResilientPostCommitActionExecutor(PostCommitRetryOptions.NoRetry);
        var action = Substitute.For<IAsyncPostCommitAction>();
        var events = new List<JsonEvent>();
        var document = Substitute.For<IObjectDocument>();

        // Act
        var result = await executor.ExecuteAsync(action, events, document);

        // Assert
        Assert.Contains("Proxy", result.ActionName); // NSubstitute creates proxy types
    }
}
