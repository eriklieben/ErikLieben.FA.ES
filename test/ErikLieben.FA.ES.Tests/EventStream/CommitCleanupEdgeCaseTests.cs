using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.Notifications;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.Tests.EventStream;

/// <summary>
/// Edge case tests for commit cleanup flow to ensure streams never reach a bad state.
/// These tests focus on:
/// - Automatic cleanup success/failure paths
/// - Rollback history persistence
/// - Broken stream state handling
/// - Version consistency after failures
/// </summary>
public class CommitCleanupEdgeCaseTests
{
    #region Test Helpers

    private static SessionDependencies CreateDependencies()
    {
        var eventStream = Substitute.For<IEventStream>();
        var eventTypeRegistry = new EventTypeRegistry();
        eventStream.EventTypeRegistry.Returns(eventTypeRegistry);
        var document = Substitute.For<IObjectDocument>();
        var active = new StreamInformation();
        document.Active.Returns(active);

        var dataStore = Substitute.For<IDataStore, IDataStoreRecovery>();
        var documentFactory = Substitute.For<IObjectDocumentFactory>();

        document.TerminatedStreams.Returns([]);

        return new SessionDependencies
        {
            EventStream = eventStream,
            Document = document,
            Active = active,
            DataStore = dataStore,
            DocumentFactory = documentFactory
        };
    }

    private static LeasedSession CreateSut(SessionDependencies dependencies)
    {
        return new LeasedSession(
            dependencies.EventStream,
            dependencies.Document,
            dependencies.DataStore,
            dependencies.DocumentFactory,
            Array.Empty<IStreamDocumentChunkClosedNotification>(),
            Array.Empty<IAsyncPostCommitAction>(),
            Array.Empty<IPreAppendAction>(),
            Array.Empty<IPostReadAction>());
    }

    private sealed class SessionDependencies
    {
        public IEventStream EventStream { get; set; } = null!;
        public IObjectDocument Document { get; set; } = null!;
        public StreamInformation Active { get; set; } = null!;
        public IDataStore DataStore { get; set; } = null!;
        public IObjectDocumentFactory DocumentFactory { get; set; } = null!;
    }

    #endregion

    #region Automatic Cleanup Success Tests

    [Fact]
    public async Task Should_cleanup_partial_writes_and_set_EventsMayBeWritten_to_false()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 5;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 7 });

        // Document succeeds, append fails
        dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
            .Returns(Task.CompletedTask);
        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Network timeout"));

        // Cleanup succeeds
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), 4, 5)
            .Returns(Task.FromResult(2));

        // Act
        var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - cleanup succeeded, so EventsMayBeWritten should be false
        Assert.False(exception.EventsMayBeWritten);
        Assert.Contains("Safe to retry", exception.Message);
        Assert.DoesNotContain("broken", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public async Task Should_record_rollback_in_history_when_cleanup_succeeds()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 10;
        var beforeCleanup = DateTimeOffset.UtcNow;
        var sut = CreateSut(dependencies);

        for (int i = 0; i < 5; i++)
        {
            sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 11 + i });
        }

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new TimeoutException("Storage timeout"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Task.FromResult(3));

        // Act
        await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - rollback history should be recorded
        Assert.NotNull(dependencies.Active.RollbackHistory);
        Assert.Single(dependencies.Active.RollbackHistory);

        var record = dependencies.Active.RollbackHistory[0];
        Assert.True(record.RolledBackAt >= beforeCleanup);
        Assert.Equal(6, record.FromVersion); // originalVersion (5) + 1
        Assert.Equal(10, record.ToVersion); // originalVersion (5) + 5 events
        Assert.Equal(3, record.EventsRemoved);
        Assert.Contains("Storage timeout", record.OriginalError);
        Assert.Equal(typeof(TimeoutException).FullName, record.OriginalExceptionType);
    }

    [Fact]
    public async Task Should_accumulate_rollback_history_across_multiple_failures()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 5;
        dependencies.Active.RollbackHistory =
        [
            new RollbackRecord
            {
                FromVersion = 1,
                ToVersion = 2,
                EventsRemoved = 2,
                RolledBackAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
        ];

        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Failed"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Task.FromResult(1));

        // Act
        await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - should have 2 records now
        Assert.NotNull(dependencies.Active.RollbackHistory);
        Assert.Equal(2, dependencies.Active.RollbackHistory.Count);
        Assert.Equal(1, dependencies.Active.RollbackHistory[0].FromVersion); // Original
        Assert.Equal(5, dependencies.Active.RollbackHistory[1].FromVersion); // New
    }

    #endregion

    #region Cleanup Failure Tests

    [Fact]
    public async Task Should_throw_CommitCleanupFailedException_when_cleanup_fails()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 5;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });

        var originalException = new InvalidOperationException("Append failed");
        var cleanupException = new InvalidOperationException("Storage unavailable");

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(originalException);
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .ThrowsAsync(cleanupException);

        // Act
        var exception = await Assert.ThrowsAsync<CommitCleanupFailedException>(() => sut.CommitAsync());

        // Assert
        Assert.Equal("test-stream", exception.StreamIdentifier);
        Assert.Equal(4, exception.OriginalVersion);
        Assert.Equal(5, exception.AttemptedVersion);
        Assert.Equal(5, exception.CleanupFromVersion);
        Assert.Equal(5, exception.CleanupToVersion);
        Assert.Same(cleanupException, exception.CleanupException);
        Assert.Same(originalException, exception.OriginalCommitException);
    }

    [Fact]
    public async Task Should_mark_stream_as_broken_when_cleanup_fails()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 10;
        var beforeFailure = DateTimeOffset.UtcNow;
        var sut = CreateSut(dependencies);

        for (int i = 0; i < 3; i++)
        {
            sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 11 + i });
        }

        var originalException = new TimeoutException("Original timeout");
        var cleanupException = new InvalidOperationException("Cleanup also failed");

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(originalException);
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .ThrowsAsync(cleanupException);

        // Act
        await Assert.ThrowsAsync<CommitCleanupFailedException>(() => sut.CommitAsync());

        // Assert - stream should be marked as broken
        Assert.True(dependencies.Active.IsBroken);
        Assert.NotNull(dependencies.Active.BrokenInfo);

        var brokenInfo = dependencies.Active.BrokenInfo!;
        Assert.True(brokenInfo.BrokenAt >= beforeFailure);
        Assert.Equal(8, brokenInfo.OrphanedFromVersion); // originalVersion (7) + 1
        Assert.Equal(10, brokenInfo.OrphanedToVersion); // originalVersion (7) + 3 events
        Assert.Contains("Cleanup also failed", brokenInfo.ErrorMessage);
        Assert.Equal(typeof(TimeoutException).FullName, brokenInfo.OriginalExceptionType);
        Assert.Equal(typeof(InvalidOperationException).FullName, brokenInfo.CleanupExceptionType);
    }

    [Fact]
    public async Task Should_attempt_to_persist_broken_state()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 5;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });

        // First SetAsync succeeds (initial commit), then we track second call
        var setAsyncCallCount = 0;
        dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
            .Returns(_ =>
            {
                setAsyncCallCount++;
                return Task.CompletedTask;
            });

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Append failed"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("Cleanup failed"));

        // Act
        await Assert.ThrowsAsync<CommitCleanupFailedException>(() => sut.CommitAsync());

        // Assert - SetAsync should be called twice: once for commit, once for broken state
        Assert.Equal(2, setAsyncCallCount);
    }

    [Fact]
    public async Task Should_still_throw_CommitCleanupFailedException_when_persisting_broken_state_fails()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 5;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });

        var setAsyncCallCount = 0;
        dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
            .Returns(_ =>
            {
                setAsyncCallCount++;
                if (setAsyncCallCount == 2)
                {
                    // Persisting broken state fails
                    throw new InvalidOperationException("Cannot persist broken state");
                }
                return Task.CompletedTask;
            });

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Append failed"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("Cleanup failed"));

        // Act - should still throw CommitCleanupFailedException, not the persist exception
        var exception = await Assert.ThrowsAsync<CommitCleanupFailedException>(() => sut.CommitAsync());

        // Assert - broken state is still set in memory even if persist failed
        Assert.True(dependencies.Active.IsBroken);
        Assert.NotNull(dependencies.Active.BrokenInfo);
    }

    #endregion

    #region Version Consistency Tests

    [Fact]
    public async Task Should_restore_version_after_cleanup_success()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.CurrentStreamVersion = 10;
        var sut = CreateSut(dependencies);

        for (int i = 0; i < 5; i++)
        {
            sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 11 + i });
        }

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Failed"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Task.FromResult(5));

        // Act
        await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - version should be restored to original (before buffer was added)
        Assert.Equal(5, dependencies.Active.CurrentStreamVersion); // 10 - 5 events
    }

    [Fact]
    public async Task Should_restore_version_after_cleanup_failure()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.CurrentStreamVersion = 10;
        var sut = CreateSut(dependencies);

        for (int i = 0; i < 5; i++)
        {
            sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 11 + i });
        }

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Failed"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("Cleanup also failed"));

        // Act
        await Assert.ThrowsAsync<CommitCleanupFailedException>(() => sut.CommitAsync());

        // Assert - version should still be restored even though cleanup failed
        Assert.Equal(5, dependencies.Active.CurrentStreamVersion);
    }

    [Fact]
    public async Task Should_calculate_correct_cleanup_range_for_single_event()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.CurrentStreamVersion = 42;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 43 });

        int capturedFromVersion = 0;
        int capturedToVersion = 0;

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Failed"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(
            Arg.Any<IObjectDocument>(),
            Arg.Do<int>(v => capturedFromVersion = v),
            Arg.Do<int>(v => capturedToVersion = v))
            .Returns(Task.FromResult(1));

        // Act
        await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - single event range
        Assert.Equal(42, capturedFromVersion); // originalVersion (41) + 1
        Assert.Equal(42, capturedToVersion); // originalVersion (41) + 1 event
    }

    [Fact]
    public async Task Should_calculate_correct_cleanup_range_for_many_events()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.CurrentStreamVersion = 100;
        var sut = CreateSut(dependencies);

        for (int i = 0; i < 50; i++)
        {
            sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 101 + i });
        }

        int capturedFromVersion = 0;
        int capturedToVersion = 0;

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Failed"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(
            Arg.Any<IObjectDocument>(),
            Arg.Do<int>(v => capturedFromVersion = v),
            Arg.Do<int>(v => capturedToVersion = v))
            .Returns(Task.FromResult(50));

        // Act
        await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - 50 event range
        Assert.Equal(51, capturedFromVersion); // originalVersion (50) + 1
        Assert.Equal(100, capturedToVersion); // originalVersion (50) + 50 events
    }

    #endregion

    #region No Cleanup Needed Tests

    [Fact]
    public async Task Should_not_attempt_cleanup_when_document_update_fails()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 5;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });

        dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
            .ThrowsAsync(new InvalidOperationException("Document update failed"));

        // Act
        var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - cleanup should NOT be called because document failed before events
        await ((IDataStoreRecovery)dependencies.DataStore).DidNotReceive().RemoveEventsForFailedCommitAsync(
            Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>());
        Assert.False(exception.EventsMayBeWritten);
        Assert.False(dependencies.Active.IsBroken);
        Assert.Null(dependencies.Active.BrokenInfo);
    }

    [Fact]
    public async Task Should_not_mark_stream_broken_when_document_update_fails()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 1 });

        dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
            .ThrowsAsync(new InvalidOperationException("Concurrency conflict"));

        // Act
        await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - stream should NOT be broken
        Assert.False(dependencies.Active.IsBroken);
        Assert.Null(dependencies.Active.BrokenInfo);
        Assert.Null(dependencies.Active.RollbackHistory);
    }

    #endregion

    #region Boundary Condition Tests

    [Fact]
    public async Task Should_handle_cleanup_returning_zero_events_removed()
    {
        // Arrange - scenario where no events were actually written before failure
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 5;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Failed before writing"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Task.FromResult(0)); // No events were actually there

        // Act
        var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - should still succeed cleanup with 0 removed
        Assert.False(exception.EventsMayBeWritten);
        Assert.NotNull(dependencies.Active.RollbackHistory);
        Assert.Single(dependencies.Active.RollbackHistory);
        Assert.Equal(0, dependencies.Active.RollbackHistory[0].EventsRemoved);
    }

    [Fact]
    public async Task Should_handle_cleanup_returning_partial_removal()
    {
        // Arrange - scenario where only some events were written
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "test-stream";
        dependencies.Active.CurrentStreamVersion = 10;
        var sut = CreateSut(dependencies);

        for (int i = 0; i < 5; i++)
        {
            sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 11 + i });
        }

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Failed midway"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Task.FromResult(2)); // Only 2 of 5 events existed

        // Act
        var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - cleanup succeeded even with partial removal
        Assert.False(exception.EventsMayBeWritten);
        Assert.NotNull(dependencies.Active.RollbackHistory);
        Assert.Single(dependencies.Active.RollbackHistory);
        Assert.Equal(2, dependencies.Active.RollbackHistory[0].EventsRemoved);
        Assert.Equal(6, dependencies.Active.RollbackHistory[0].FromVersion);
        Assert.Equal(10, dependencies.Active.RollbackHistory[0].ToVersion);
    }

    [Fact]
    public async Task Should_handle_version_starting_at_zero()
    {
        // Arrange - first events ever on stream
        var dependencies = CreateDependencies();
        dependencies.Active.StreamIdentifier = "new-stream";
        dependencies.Active.CurrentStreamVersion = 2;
        var sut = CreateSut(dependencies);

        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 3 });
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 4 });
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 5 });

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new InvalidOperationException("Failed"));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Task.FromResult(3));

        // Act
        var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert
        Assert.False(exception.EventsMayBeWritten);
        Assert.Equal(0, dependencies.Active.RollbackHistory![0].FromVersion);
        Assert.Equal(2, dependencies.Active.RollbackHistory![0].ToVersion);
    }

    #endregion

    #region Error Message Preservation Tests

    [Fact]
    public async Task Should_preserve_original_error_message_in_rollback_record()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.CurrentStreamVersion = 5;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });

        var specificErrorMessage = "Network timeout after 30 seconds connecting to storage.blob.core.windows.net";
        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(new TimeoutException(specificErrorMessage));
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Task.FromResult(1));

        // Act
        await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

        // Assert - error message should be preserved
        Assert.NotNull(dependencies.Active.RollbackHistory);
        Assert.Equal(specificErrorMessage, dependencies.Active.RollbackHistory[0].OriginalError);
        Assert.Equal(typeof(TimeoutException).FullName, dependencies.Active.RollbackHistory[0].OriginalExceptionType);
    }

    [Fact]
    public async Task Should_preserve_both_exceptions_in_CommitCleanupFailedException()
    {
        // Arrange
        var dependencies = CreateDependencies();
        dependencies.Active.CurrentStreamVersion = 5;
        var sut = CreateSut(dependencies);
        sut.Buffer.Add(new JsonEvent { EventType = "Test", EventVersion = 6 });

        var originalException = new InvalidOperationException("First failure");
        var cleanupException = new TimeoutException("Cleanup timeout");

        dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
            .ThrowsAsync(originalException);
        ((IDataStoreRecovery)dependencies.DataStore).RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
            .ThrowsAsync(cleanupException);

        // Act
        var exception = await Assert.ThrowsAsync<CommitCleanupFailedException>(() => sut.CommitAsync());

        // Assert - both exceptions accessible
        Assert.Same(originalException, exception.OriginalCommitException);
        Assert.Same(cleanupException, exception.CleanupException);
        Assert.Same(originalException, exception.InnerException);

        // Error message should contain info about both failures
        Assert.Contains("First failure", exception.Message);
        Assert.Contains("Cleanup timeout", exception.Message);
    }

    #endregion
}
