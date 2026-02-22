using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class PostCommitActionFailedExceptionTests
{
    [Fact]
    public void Should_contain_stream_id()
    {
        // Arrange
        var streamId = "test-stream-123";
        var events = CreateTestEvents(3);
        var failedActions = CreateFailedActions(1);
        var succeededActions = CreateSucceededActions(1);

        // Act
        var sut = new PostCommitActionFailedException(streamId, events, failedActions, succeededActions);

        // Assert
        Assert.Equal(streamId, sut.StreamId);
    }

    [Fact]
    public void Should_contain_committed_events()
    {
        // Arrange
        var events = CreateTestEvents(5);
        var failedActions = CreateFailedActions(1);
        var succeededActions = CreateSucceededActions(1);

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, succeededActions);

        // Assert
        Assert.Equal(5, sut.CommittedEvents.Count);
    }

    [Fact]
    public void Should_contain_failed_and_succeeded_actions()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var failedActions = CreateFailedActions(2);
        var succeededActions = CreateSucceededActions(3);

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, succeededActions);

        // Assert
        Assert.Equal(2, sut.FailedActions.Count);
        Assert.Equal(3, sut.SucceededActions.Count);
    }

    [Fact]
    public void Should_calculate_version_range()
    {
        // Arrange
        var events = new List<JsonEvent>
        {
            new JsonEvent { EventType = "Test", EventVersion = 5 },
            new JsonEvent { EventType = "Test", EventVersion = 6 },
            new JsonEvent { EventType = "Test", EventVersion = 7 }
        };
        var failedActions = CreateFailedActions(1);

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, []);

        // Assert
        Assert.Equal((5, 7), sut.CommittedVersionRange);
    }

    [Fact]
    public void Should_have_zero_version_range_when_no_events()
    {
        // Arrange
        var events = new List<JsonEvent>();
        var failedActions = CreateFailedActions(1);

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, []);

        // Assert
        Assert.Equal((0, 0), sut.CommittedVersionRange);
    }

    [Fact]
    public void Should_provide_failed_action_names()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var failedActions = new List<FailedPostCommitAction>
        {
            new("Action1", typeof(object), new Exception(), 1, TimeSpan.Zero),
            new("Action2", typeof(object), new Exception(), 1, TimeSpan.Zero)
        };

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, []);

        // Assert
        Assert.Contains("Action1", sut.FailedActionNames);
        Assert.Contains("Action2", sut.FailedActionNames);
    }

    [Fact]
    public void Should_provide_succeeded_action_names()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var failedActions = CreateFailedActions(1);
        var succeededActions = new List<SucceededPostCommitAction>
        {
            new("SuccessAction1", typeof(object), TimeSpan.Zero),
            new("SuccessAction2", typeof(object), TimeSpan.Zero)
        };

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, succeededActions);

        // Assert
        Assert.Contains("SuccessAction1", sut.SucceededActionNames);
        Assert.Contains("SuccessAction2", sut.SucceededActionNames);
    }

    [Fact]
    public void Should_provide_first_error()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var firstError = new InvalidOperationException("First error");
        var failedActions = new List<FailedPostCommitAction>
        {
            new("Action1", typeof(object), firstError, 1, TimeSpan.Zero),
            new("Action2", typeof(object), new Exception("Second"), 1, TimeSpan.Zero)
        };

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, []);

        // Assert
        Assert.Equal(firstError, sut.FirstError);
    }

    [Fact]
    public void Should_return_null_first_error_when_no_failures()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var emptyFailedActions = new List<FailedPostCommitAction>();

        // Act
        var sut = new PostCommitActionFailedException("stream", events, emptyFailedActions, []);

        // Assert
        Assert.Null(sut.FirstError);
    }

    [Fact]
    public void Should_include_error_code_in_message()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var failedActions = CreateFailedActions(1);

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, []);

        // Assert
        Assert.Contains(PostCommitActionFailedException.PostCommitErrorCode, sut.Message);
    }

    [Fact]
    public void Should_include_stream_id_in_message()
    {
        // Arrange
        var streamId = "my-unique-stream";
        var events = CreateTestEvents(1);
        var failedActions = CreateFailedActions(1);

        // Act
        var sut = new PostCommitActionFailedException(streamId, events, failedActions, []);

        // Assert
        Assert.Contains(streamId, sut.Message);
    }

    [Fact]
    public void Should_include_counts_in_message()
    {
        // Arrange
        var events = CreateTestEvents(5);
        var failedActions = CreateFailedActions(2);
        var succeededActions = CreateSucceededActions(3);

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, succeededActions);

        // Assert
        Assert.Contains("Failed: 2", sut.Message);
        Assert.Contains("Succeeded: 3", sut.Message);
        Assert.Contains("5 events", sut.Message);
    }

    [Fact]
    public void Should_have_correct_error_code_constant()
    {
        // Assert
        Assert.Equal("ELFAES-POSTCOMMIT-0001", PostCommitActionFailedException.PostCommitErrorCode);
    }

    [Fact]
    public void Should_inherit_from_EsException()
    {
        // Arrange
        var events = CreateTestEvents(1);
        var failedActions = CreateFailedActions(1);

        // Act
        var sut = new PostCommitActionFailedException("stream", events, failedActions, []);

        // Assert
        Assert.IsType<EsException>(sut, exactMatch: false);
    }

    private static List<JsonEvent> CreateTestEvents(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new JsonEvent { EventType = "TestEvent", EventVersion = i })
            .ToList();
    }

    private static List<FailedPostCommitAction> CreateFailedActions(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new FailedPostCommitAction(
                $"FailedAction{i}",
                typeof(object),
                new Exception($"Error {i}"),
                1,
                TimeSpan.Zero))
            .ToList();
    }

    private static List<SucceededPostCommitAction> CreateSucceededActions(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new SucceededPostCommitAction(
                $"SucceededAction{i}",
                typeof(object),
                TimeSpan.Zero))
            .ToList();
    }
}
