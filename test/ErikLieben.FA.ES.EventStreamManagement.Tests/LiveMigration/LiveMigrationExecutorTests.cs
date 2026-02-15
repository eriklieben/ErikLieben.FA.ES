using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Events;
using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.LiveMigration;

public class LiveMigrationExecutorTests
{
    private static LiveMigrationContext CreateContext(
        IDataStore? dataStore = null,
        IDocumentStore? documentStore = null,
        LiveMigrationOptions? options = null)
    {
        var sourceStreamInfo = new StreamInformation
        {
            StreamIdentifier = "source-stream",
            StreamType = "blob",
            CurrentStreamVersion = 0,
            DataStore = "default"
        };

        var targetStreamInfo = new StreamInformation
        {
            StreamIdentifier = "target-stream",
            StreamType = "blob",
            CurrentStreamVersion = -1,
            DataStore = "default"
        };

        var sourceDocument = Substitute.For<IObjectDocument>();
        sourceDocument.ObjectId.Returns("test-object");
        sourceDocument.ObjectName.Returns("TestObject");
        sourceDocument.Active.Returns(sourceStreamInfo);
        sourceDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

        var targetDocument = Substitute.For<IObjectDocument>();
        targetDocument.ObjectId.Returns("test-object");
        targetDocument.ObjectName.Returns("TestObject");
        targetDocument.Active.Returns(targetStreamInfo);
        targetDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

        // Set up document store to return the source document when GetAsync is called
        // (needed for AttemptCloseAsync which reloads the document to get fresh hash)
        var docStore = documentStore ?? Substitute.For<IDocumentStore>();
        docStore.GetAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(sourceDocument);

        return new LiveMigrationContext
        {
            MigrationId = Guid.NewGuid(),
            SourceDocument = sourceDocument,
            SourceStreamId = "source-stream",
            TargetStreamId = "target-stream",
            TargetDocument = targetDocument,
            DataStore = dataStore ?? Substitute.For<IDataStore>(),
            DocumentStore = docStore,
            Options = options ?? new LiveMigrationOptions()
        };
    }

    private static IEvent CreateEvent(string type, int version)
    {
        var evt = Substitute.For<IEvent>();
        evt.EventType.Returns(type);
        evt.EventVersion.Returns(version);
        evt.Payload.Returns("{}");
        return evt;
    }

    private static ILoggerFactory CreateLoggerFactory()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<LiveMigrationExecutor>()
            .Returns(Substitute.For<ILogger<LiveMigrationExecutor>>());
        return loggerFactory;
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_context_is_null()
        {
            // Arrange
            var loggerFactory = CreateLoggerFactory();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LiveMigrationExecutor(null!, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_factory_is_null()
        {
            // Arrange
            var context = CreateContext();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LiveMigrationExecutor(context, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var context = CreateContext();
            var loggerFactory = CreateLoggerFactory();

            // Act
            var sut = new LiveMigrationExecutor(context, loggerFactory);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ExecuteAsyncMethod
    {
        [Fact]
        public async Task Should_succeed_when_source_is_empty()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns((IEnumerable<IEvent>?)null);

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.TotalEventsCopied);
            Assert.Equal(1, result.Iterations);
        }

        [Fact]
        public async Task Should_copy_events_from_source_to_target()
        {
            // Arrange
            var sourceEvents = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 1),
                CreateEvent("Event3", 2)
            };

            var dataStore = Substitute.For<IDataStore>();

            // First call returns source events, subsequent calls return empty (target)
            var callCount = 0;
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    callCount++;
                    var doc = callInfo.Arg<IObjectDocument>();

                    // Source reads return events, then after close check return closed event
                    if (doc.Active.StreamIdentifier == "source-stream")
                    {
                        if (callCount <= 2)
                            return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                        return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                    }

                    // Target reads - after first copy should have events
                    if (callCount > 2)
                        return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(null);
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3, result.TotalEventsCopied);
            await dataStore.Received().AppendAsync(
                Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IEvent[]>());
        }

        [Fact]
        public async Task Should_not_copy_close_events()
        {
            // Arrange - source has events including a close event
            var sourceEventsWithClose = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 1),
                CreateEvent(StreamClosedEvent.EventTypeName, 2)
            };

            var dataStore = Substitute.For<IDataStore>();

            // Only capture events written to TARGET stream
            var targetAppendedEvents = new List<IEvent>();
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    var events = callInfo.Arg<IEvent[]>();
                    targetAppendedEvents.AddRange(events);
                    return Task.CompletedTask;
                });

            // Source append (for close event) should succeed
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    // Source has close event
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>(sourceEventsWithClose);
                    // Target returns what was appended (business events only)
                    return Task.FromResult<IEnumerable<IEvent>?>(targetAppendedEvents.Count > 0 ? targetAppendedEvents : null);
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert - should only copy 2 events (Event1, Event2), not the close event
            Assert.True(result.Success);
            Assert.Equal(2, result.TotalEventsCopied);
            Assert.NotEmpty(targetAppendedEvents);
            Assert.DoesNotContain(targetAppendedEvents, e => e.EventType == StreamClosedEvent.EventTypeName);
        }

        [Fact]
        public async Task Should_invoke_progress_callback()
        {
            // Arrange
            var progressReports = new List<LiveMigrationProgress>();

            var sourceEvents = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 1)
            };

            var dataStore = Substitute.For<IDataStore>();
            var readCount = 0;
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(_ =>
                {
                    readCount++;
                    if (readCount <= 2)
                        return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                });

            var documentStore = Substitute.For<IDocumentStore>();

            var options = new LiveMigrationOptions();
            options.OnCatchUpProgress(p => progressReports.Add(p));

            var context = CreateContext(dataStore, documentStore, options);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(progressReports);
            Assert.All(progressReports, p => Assert.True(p.Iteration > 0));
        }

        [Fact]
        public async Task Should_fail_when_max_iterations_exceeded()
        {
            // Arrange - source keeps getting new events
            var eventVersion = 0;
            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(_ =>
                {
                    // Source keeps growing
                    eventVersion++;
                    var events = Enumerable.Range(0, eventVersion)
                        .Select(i => CreateEvent($"Event{i}", i))
                        .ToList();
                    return Task.FromResult<IEnumerable<IEvent>?>(events);
                });

            var documentStore = Substitute.For<IDocumentStore>();

            var options = new LiveMigrationOptions();
            options.WithMaxIterations(3);
            options.WithCatchUpDelay(TimeSpan.Zero);

            var context = CreateContext(dataStore, documentStore, options);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.False(result.Success);
            Assert.Contains("maximum iterations", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Should_fail_when_timeout_exceeded()
        {
            // Arrange - source keeps getting new events during close attempt,
            // causing version conflicts that force retries until timeout.
            // The AttemptCloseAsync method re-reads source to verify version hasn't changed.
            // We simulate new events appearing between reads to cause conflicts.
            var sourceReadCount = 0;

            var dataStore = Substitute.For<IDataStore>();

            // AppendAsync to target - we won't get here
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            // AppendAsync to source - won't be called because version check fails
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(async callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();

                    if (doc.Active.StreamIdentifier == "source-stream")
                    {
                        sourceReadCount++;
                        // Add delay to help timeout
                        await Task.Delay(20);

                        // Odd reads return version N, even reads return N+1
                        // This causes CatchUp to see version V, but AttemptClose re-read sees V+1
                        var version = (sourceReadCount + 1) / 2; // 1->1, 2->1, 3->2, 4->2, 5->3...
                        var events = Enumerable.Range(0, version + 1)
                            .Select(i => CreateEvent($"Event{i}", i))
                            .ToList();
                        return (IEnumerable<IEvent>?)events;
                    }

                    // Target matches what we think source has
                    var targetVersion = sourceReadCount / 2;
                    if (targetVersion < 0)
                        return (IEnumerable<IEvent>?)null;

                    var targetEvents = Enumerable.Range(0, targetVersion + 1)
                        .Select(i => CreateEvent($"Event{i}", i))
                        .ToList();
                    return (IEnumerable<IEvent>?)targetEvents;
                });

            var documentStore = Substitute.For<IDocumentStore>();

            var options = new LiveMigrationOptions();
            options.WithCloseTimeout(TimeSpan.FromMilliseconds(100));
            options.WithCatchUpDelay(TimeSpan.Zero);

            var context = CreateContext(dataStore, documentStore, options);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.False(result.Success);
            Assert.Contains("timeout", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Should_update_object_document_on_success()
        {
            // Arrange
            var sourceEvents = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 1)
            };

            var dataStore = Substitute.For<IDataStore>();
            var readCount = 0;
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(_ =>
                {
                    readCount++;
                    return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            await documentStore.Received().SetAsync(
                Arg.Is<IObjectDocument>(d =>
                    d.Active.StreamIdentifier == "target-stream" &&
                    d.TerminatedStreams.Any(t => t.StreamIdentifier == "source-stream")));
        }

        [Fact]
        public async Task Should_set_continuation_stream_in_terminated_stream()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(null));

            IObjectDocument? savedDocument = null;
            var documentStore = Substitute.For<IDocumentStore>();
            documentStore.SetAsync(Arg.Do<IObjectDocument>(d => savedDocument = d))
                .Returns(Task.CompletedTask);

            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(savedDocument);

            var terminatedStream = savedDocument.TerminatedStreams.FirstOrDefault();
            Assert.NotNull(terminatedStream);
            Assert.Equal("source-stream", terminatedStream.StreamIdentifier);
            Assert.Equal("target-stream", terminatedStream.ContinuationStreamId);
        }

        [Fact]
        public async Task Should_support_cancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var readCount = 0;

            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    readCount++;

                    // Cancel and throw on second read (during catch-up phase)
                    if (readCount >= 2)
                    {
                        cts.Cancel();
                        throw new OperationCanceledException(cts.Token);
                    }

                    if (doc.Active.StreamIdentifier == "source-stream")
                    {
                        // Source has events
                        var events = new[] { CreateEvent("Event0", 0), CreateEvent("Event1", 1) };
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)events);
                    }

                    // Target is empty - will trigger catch-up
                    return Task.FromResult<IEnumerable<IEvent>?>(null);
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => sut.ExecuteAsync(cts.Token));
        }

        [Fact]
        public async Task Should_return_failure_on_exception()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Throws(new InvalidOperationException("Test exception"));

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.NotNull(result.Exception);
            Assert.IsType<InvalidOperationException>(result.Exception);
        }

        [Fact]
        public async Task Should_track_elapsed_time()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(async _ =>
                {
                    await Task.Delay(50);
                    return (IEnumerable<IEvent>?)null;
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.True(result.ElapsedTime >= TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public async Task Should_return_correct_stream_ids_in_result()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(null));

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.Equal("source-stream", result.SourceStreamId);
            Assert.Equal("target-stream", result.TargetStreamId);
            Assert.Equal(context.MigrationId, result.MigrationId);
        }
    }

    public class TransformationScenarios
    {
        [Fact]
        public async Task Should_apply_transformer_to_events()
        {
            // Arrange
            var sourceEvents = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 1)
            };

            var transformedEvent1 = CreateEvent("TransformedEvent1", 0);
            var transformedEvent2 = CreateEvent("TransformedEvent2", 1);

            var transformer = Substitute.For<IEventTransformer>();
            transformer.TransformAsync(sourceEvents[0], Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(transformedEvent1));
            transformer.TransformAsync(sourceEvents[1], Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(transformedEvent2));

            var dataStore = Substitute.For<IDataStore>();
            var appendedToTarget = new List<IEvent>();

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    appendedToTarget.AddRange(callInfo.Arg<IEvent[]>());
                    return Task.CompletedTask;
                });

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(appendedToTarget.Count > 0 ? appendedToTarget : null);
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContextWithTransformer(dataStore, documentStore, transformer);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Contains(appendedToTarget, e => e.EventType == "TransformedEvent1");
            Assert.Contains(appendedToTarget, e => e.EventType == "TransformedEvent2");
        }

        [Fact]
        public async Task Should_skip_events_when_transformation_fails()
        {
            // Arrange
            var sourceEvents = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 1),
                CreateEvent("Event3", 2)
            };

            var transformer = Substitute.For<IEventTransformer>();
            transformer.TransformAsync(sourceEvents[0], Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(sourceEvents[0]));
            transformer.TransformAsync(sourceEvents[1], Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Transform failed"));
            transformer.TransformAsync(sourceEvents[2], Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(sourceEvents[2]));

            var dataStore = Substitute.For<IDataStore>();
            var appendedToTarget = new List<IEvent>();

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    appendedToTarget.AddRange(callInfo.Arg<IEvent[]>());
                    return Task.CompletedTask;
                });

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(appendedToTarget.Count > 0 ? appendedToTarget : null);
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContextWithTransformer(dataStore, documentStore, transformer);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            // Event2 should be skipped due to transformation failure
            Assert.Equal(2, appendedToTarget.Count);
            Assert.DoesNotContain(appendedToTarget, e => e.EventType == "Event2");
        }

        private static LiveMigrationContext CreateContextWithTransformer(
            IDataStore dataStore,
            IDocumentStore documentStore,
            IEventTransformer transformer)
        {
            var sourceStreamInfo = new StreamInformation
            {
                StreamIdentifier = "source-stream",
                StreamType = "blob",
                CurrentStreamVersion = 0,
                DataStore = "default"
            };

            var targetStreamInfo = new StreamInformation
            {
                StreamIdentifier = "target-stream",
                StreamType = "blob",
                CurrentStreamVersion = -1,
                DataStore = "default"
            };

            var sourceDocument = Substitute.For<IObjectDocument>();
            sourceDocument.ObjectId.Returns("test-object");
            sourceDocument.ObjectName.Returns("TestObject");
            sourceDocument.Active.Returns(sourceStreamInfo);
            sourceDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

            var targetDocument = Substitute.For<IObjectDocument>();
            targetDocument.ObjectId.Returns("test-object");
            targetDocument.ObjectName.Returns("TestObject");
            targetDocument.Active.Returns(targetStreamInfo);
            targetDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

            // Set up document store to return the source document when GetAsync is called
            // (needed for AttemptCloseAsync which reloads the document to get fresh hash)
            documentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(sourceDocument);

            return new LiveMigrationContext
            {
                MigrationId = Guid.NewGuid(),
                SourceDocument = sourceDocument,
                SourceStreamId = "source-stream",
                TargetStreamId = "target-stream",
                TargetDocument = targetDocument,
                DataStore = dataStore,
                DocumentStore = documentStore,
                Options = new LiveMigrationOptions(),
                Transformer = transformer
            };
        }
    }

    public class DataStoreErrorScenarios
    {
        [Fact]
        public async Task Should_fail_when_target_append_throws_non_version_conflict()
        {
            // Arrange
            var sourceEvents = new[] { CreateEvent("Event1", 0) };

            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(null);
                });

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .ThrowsAsync(new IOException("Storage unavailable"));

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Storage unavailable", result.Error);
            Assert.IsType<IOException>(result.Exception);
        }

        [Fact]
        public async Task Should_fail_when_document_store_update_fails_after_close()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(null));

            var documentStore = Substitute.For<IDocumentStore>();
            documentStore.SetAsync(Arg.Any<IObjectDocument>())
                .ThrowsAsync(new InvalidOperationException("Document store error"));

            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Document store error", result.Error);
        }
    }

    public class OptimisticConcurrencyScenarios
    {
        [Fact]
        public async Task Should_retry_when_close_throws_optimistic_concurrency_exception()
        {
            // Arrange
            var sourceEvents = new[] { CreateEvent("Event1", 0) };
            var closeAttemptCount = 0;

            var dataStore = Substitute.For<IDataStore>();

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(_ =>
                {
                    closeAttemptCount++;
                    if (closeAttemptCount == 1)
                    {
                        throw new OptimisticConcurrencyException("source-stream", 0, 1);
                    }
                    return Task.CompletedTask;
                });

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                });

            var documentStore = Substitute.For<IDocumentStore>();

            var options = new LiveMigrationOptions();
            options.WithCatchUpDelay(TimeSpan.Zero);

            var context = CreateContext(dataStore, documentStore, options);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.True(closeAttemptCount >= 2, $"Expected at least 2 close attempts, got {closeAttemptCount}");
        }

        [Fact]
        public async Task Should_retry_when_close_throws_exception_with_conflict_keyword()
        {
            // Arrange
            var sourceEvents = new[] { CreateEvent("Event1", 0) };
            var closeAttemptCount = 0;

            var dataStore = Substitute.For<IDataStore>();

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(_ =>
                {
                    closeAttemptCount++;
                    if (closeAttemptCount == 1)
                    {
                        throw new InvalidOperationException("ETag conflict detected");
                    }
                    return Task.CompletedTask;
                });

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                });

            var documentStore = Substitute.For<IDocumentStore>();

            var options = new LiveMigrationOptions();
            options.WithCatchUpDelay(TimeSpan.Zero);

            var context = CreateContext(dataStore, documentStore, options);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.True(closeAttemptCount >= 2, $"Expected at least 2 close attempts, got {closeAttemptCount}");
        }
    }

    public class EdgeVersionScenarios
    {
        [Fact]
        public async Task Should_handle_target_with_existing_events()
        {
            // Arrange - target already has some events from a previous partial migration
            var sourceEvents = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 1),
                CreateEvent("Event3", 2)
            };
            var existingTargetEvents = new[]
            {
                CreateEvent("Event1", 0) // Already migrated
            };

            var dataStore = Substitute.For<IDataStore>();
            var appendedToTarget = new List<IEvent>();

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    appendedToTarget.AddRange(callInfo.Arg<IEvent[]>());
                    return Task.CompletedTask;
                });

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    // Target starts with existing events, then includes newly appended
                    var allTargetEvents = existingTargetEvents.Concat(appendedToTarget).ToList();
                    return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)allTargetEvents);
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            // Should only copy Event2 and Event3 (Event1 already exists)
            Assert.Equal(2, result.TotalEventsCopied);
            Assert.DoesNotContain(appendedToTarget, e => e.EventVersion == 0);
        }

        [Fact]
        public async Task Should_handle_events_with_non_sequential_versions()
        {
            // Arrange - events have gaps in version numbers
            var sourceEvents = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 5),  // Gap in versions
                CreateEvent("Event3", 10)
            };

            var dataStore = Substitute.For<IDataStore>();
            var appendedToTarget = new List<IEvent>();

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    appendedToTarget.AddRange(callInfo.Arg<IEvent[]>());
                    return Task.CompletedTask;
                });

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(appendedToTarget.Count > 0 ? appendedToTarget : null);
                });

            var documentStore = Substitute.For<IDocumentStore>();
            var context = CreateContext(dataStore, documentStore);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3, result.TotalEventsCopied);
        }
    }

    public class ExistingTerminatedStreamsScenarios
    {
        [Fact]
        public async Task Should_preserve_existing_terminated_streams()
        {
            // Arrange
            var existingTerminatedStream = new TerminatedStream
            {
                StreamIdentifier = "old-stream",
                StreamType = "blob",
                Reason = "Previous migration",
                ContinuationStreamId = "source-stream"
            };

            var sourceStreamInfo = new StreamInformation
            {
                StreamIdentifier = "source-stream",
                StreamType = "blob",
                CurrentStreamVersion = 0,
                DataStore = "default"
            };

            var targetStreamInfo = new StreamInformation
            {
                StreamIdentifier = "target-stream",
                StreamType = "blob",
                CurrentStreamVersion = -1,
                DataStore = "default"
            };

            var sourceDocument = Substitute.For<IObjectDocument>();
            sourceDocument.ObjectId.Returns("test-object");
            sourceDocument.ObjectName.Returns("TestObject");
            sourceDocument.Active.Returns(sourceStreamInfo);
            sourceDocument.TerminatedStreams.Returns(new List<TerminatedStream> { existingTerminatedStream });

            var targetDocument = Substitute.For<IObjectDocument>();
            targetDocument.ObjectId.Returns("test-object");
            targetDocument.ObjectName.Returns("TestObject");
            targetDocument.Active.Returns(targetStreamInfo);
            targetDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

            var dataStore = Substitute.For<IDataStore>();
            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(null));

            IObjectDocument? savedDocument = null;
            var documentStore = Substitute.For<IDocumentStore>();
            documentStore.SetAsync(Arg.Do<IObjectDocument>(d => savedDocument = d))
                .Returns(Task.CompletedTask);
            // Set up document store to return the source document when GetAsync is called
            documentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(sourceDocument);

            var context = new LiveMigrationContext
            {
                MigrationId = Guid.NewGuid(),
                SourceDocument = sourceDocument,
                SourceStreamId = "source-stream",
                TargetStreamId = "target-stream",
                TargetDocument = targetDocument,
                DataStore = dataStore,
                DocumentStore = documentStore,
                Options = new LiveMigrationOptions()
            };

            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(savedDocument);
            Assert.Equal(2, savedDocument.TerminatedStreams.Count);
            Assert.Contains(savedDocument.TerminatedStreams, t => t.StreamIdentifier == "old-stream");
            Assert.Contains(savedDocument.TerminatedStreams, t => t.StreamIdentifier == "source-stream");
        }
    }

    public class CloseEventVerificationScenarios
    {
        [Fact]
        public async Task Should_write_close_event_with_correct_metadata()
        {
            // Arrange
            var sourceEvents = new[] { CreateEvent("Event1", 0), CreateEvent("Event2", 1) };
            IEvent? closeEvent = null;
            var migrationId = Guid.NewGuid();

            var dataStore = Substitute.For<IDataStore>();
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Do<IEvent[]>(events => closeEvent = events.FirstOrDefault()))
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(sourceEvents);
                });

            var sourceStreamInfo = new StreamInformation
            {
                StreamIdentifier = "source-stream",
                StreamType = "blob",
                CurrentStreamVersion = 0,
                DataStore = "source-data-store"
            };

            var targetStreamInfo = new StreamInformation
            {
                StreamIdentifier = "target-stream",
                StreamType = "cosmos",
                CurrentStreamVersion = -1,
                DataStore = "target-data-store",
                DocumentStore = "target-doc-store"
            };

            var sourceDocument = Substitute.For<IObjectDocument>();
            sourceDocument.ObjectId.Returns("test-object");
            sourceDocument.ObjectName.Returns("TestObject");
            sourceDocument.Active.Returns(sourceStreamInfo);
            sourceDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

            var targetDocument = Substitute.For<IObjectDocument>();
            targetDocument.ObjectId.Returns("test-object");
            targetDocument.ObjectName.Returns("TestObject");
            targetDocument.Active.Returns(targetStreamInfo);
            targetDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

            var documentStore = Substitute.For<IDocumentStore>();
            // Set up document store to return the source document when GetAsync is called
            documentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(sourceDocument);

            var context = new LiveMigrationContext
            {
                MigrationId = migrationId,
                SourceDocument = sourceDocument,
                SourceStreamId = "source-stream",
                TargetStreamId = "target-stream",
                TargetDocument = targetDocument,
                DataStore = dataStore,
                DocumentStore = documentStore,
                Options = new LiveMigrationOptions()
            };

            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(closeEvent);
            Assert.Equal(StreamClosedEvent.EventTypeName, closeEvent.EventType);
            Assert.Equal(2, closeEvent.EventVersion); // After Event1(0) and Event2(1)

            // Verify payload contains expected fields
            Assert.Contains("target-stream", closeEvent.Payload);
            Assert.Contains(migrationId.ToString(), closeEvent.Payload);
        }
    }

    public class ProgressReportingVerificationScenarios
    {
        [Fact]
        public async Task Should_report_accurate_progress_values()
        {
            // Arrange
            var progressReports = new List<LiveMigrationProgress>();
            var sourceEvents = new[]
            {
                CreateEvent("Event1", 0),
                CreateEvent("Event2", 1),
                CreateEvent("Event3", 2)
            };

            var dataStore = Substitute.For<IDataStore>();
            var targetEvents = new List<IEvent>();

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    targetEvents.AddRange(callInfo.Arg<IEvent[]>());
                    return Task.CompletedTask;
                });

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();
                    if (doc.Active.StreamIdentifier == "source-stream")
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)sourceEvents);
                    return Task.FromResult<IEnumerable<IEvent>?>(targetEvents.Count > 0 ? targetEvents : null);
                });

            var documentStore = Substitute.For<IDocumentStore>();

            var options = new LiveMigrationOptions();
            options.OnCatchUpProgress(p => progressReports.Add(p));

            var context = CreateContext(dataStore, documentStore, options);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(progressReports);

            var firstReport = progressReports.First();
            Assert.Equal(1, firstReport.Iteration);
            Assert.Equal(2, firstReport.SourceVersion); // Max version in source
            Assert.True(firstReport.ElapsedTime > TimeSpan.Zero);
        }
    }

    public class LateEventCatchUpScenarios
    {
        [Fact]
        public async Task Should_catch_up_events_added_after_close_event_is_written()
        {
            // Arrange
            // This test simulates the race condition where events are added to the source stream
            // between the version check and the close event being appended.
            // After the close event is written, the executor should verify and catch up any late events.
            var sourceReadCount = 0;
            var closeEventWritten = false;

            var dataStore = Substitute.For<IDataStore>();
            var appendedToTarget = new List<IEvent>();

            // Track appends to target
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    appendedToTarget.AddRange(callInfo.Arg<IEvent[]>());
                    return Task.CompletedTask;
                });

            // Close event append to source
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    closeEventWritten = true;
                    return Task.CompletedTask;
                });

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();

                    if (doc.Active.StreamIdentifier == "source-stream")
                    {
                        sourceReadCount++;

                        // Flow:
                        // Read 1: CatchUpAsync reads source
                        // Read 2: AttemptCloseAsync initial verify
                        // Close event is written -> closeEventWritten = true
                        // Read 3: Post-close verification (my new code)
                        // After close event is written, post-close verification read shows 3 events (v0, v1, v2)
                        // This simulates a late event (v2) being added during the close
                        if (closeEventWritten)
                        {
                            // Post-close verification: show the late event
                            var events = new[]
                            {
                                CreateEvent("Event0", 0),
                                CreateEvent("Event1", 1),
                                CreateEvent("LateEvent", 2)  // Event added during close
                            };
                            return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)events);
                        }

                        // Pre-close reads: just 2 events
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)new[]
                        {
                            CreateEvent("Event0", 0),
                            CreateEvent("Event1", 1)
                        });
                    }

                    // Target stream: return what was appended
                    if (appendedToTarget.Count > 0)
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)appendedToTarget.ToList());
                    return Task.FromResult<IEnumerable<IEvent>?>(null);
                });

            var documentStore = Substitute.For<IDocumentStore>();

            var options = new LiveMigrationOptions();
            options.WithCatchUpDelay(TimeSpan.Zero);

            var context = CreateContext(dataStore, documentStore, options);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success, $"Expected success but got error: {result.Error}");
            // Should have copied 3 events: Event0, Event1, and the late LateEvent
            Assert.Equal(3, result.TotalEventsCopied);
            Assert.Contains(appendedToTarget, e => e.EventType == "LateEvent");
        }

        [Fact]
        public async Task Should_apply_transformation_to_late_events()
        {
            // Arrange - same as above but with a transformer
            var closeEventWritten = false;
            var sourceReadCount = 0;

            var transformer = Substitute.For<IEventTransformer>();
            transformer.TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var evt = callInfo.Arg<IEvent>();
                    var transformed = Substitute.For<IEvent>();
                    transformed.EventType.Returns($"Transformed_{evt.EventType}");
                    transformed.EventVersion.Returns(evt.EventVersion);
                    transformed.Payload.Returns(evt.Payload);
                    return Task.FromResult(transformed);
                });

            var dataStore = Substitute.For<IDataStore>();
            var appendedToTarget = new List<IEvent>();

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    appendedToTarget.AddRange(callInfo.Arg<IEvent[]>());
                    return Task.CompletedTask;
                });

            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    closeEventWritten = true;
                    return Task.CompletedTask;
                });

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();

                    if (doc.Active.StreamIdentifier == "source-stream")
                    {
                        sourceReadCount++;

                        if (closeEventWritten)
                        {
                            // Post-close: show late event
                            return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)new[]
                            {
                                CreateEvent("Event0", 0),
                                CreateEvent("LateEvent", 1)
                            });
                        }

                        // Pre-close: just one event
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)new[]
                        {
                            CreateEvent("Event0", 0)
                        });
                    }

                    if (appendedToTarget.Count > 0)
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)appendedToTarget.ToList());
                    return Task.FromResult<IEnumerable<IEvent>?>(null);
                });

            var options = new LiveMigrationOptions();
            options.WithCatchUpDelay(TimeSpan.Zero);

            var sourceStreamInfo = new StreamInformation
            {
                StreamIdentifier = "source-stream",
                StreamType = "blob",
                CurrentStreamVersion = 0,
                DataStore = "default"
            };

            var targetStreamInfo = new StreamInformation
            {
                StreamIdentifier = "target-stream",
                StreamType = "blob",
                CurrentStreamVersion = -1,
                DataStore = "default"
            };

            var sourceDocument = Substitute.For<IObjectDocument>();
            sourceDocument.ObjectId.Returns("test-object");
            sourceDocument.ObjectName.Returns("TestObject");
            sourceDocument.Active.Returns(sourceStreamInfo);
            sourceDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

            var targetDocument = Substitute.For<IObjectDocument>();
            targetDocument.ObjectId.Returns("test-object");
            targetDocument.ObjectName.Returns("TestObject");
            targetDocument.Active.Returns(targetStreamInfo);
            targetDocument.TerminatedStreams.Returns(new List<TerminatedStream>());

            var documentStore = Substitute.For<IDocumentStore>();
            // Set up document store to return the source document when GetAsync is called
            documentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>())
                .Returns(sourceDocument);

            var context = new LiveMigrationContext
            {
                MigrationId = Guid.NewGuid(),
                SourceDocument = sourceDocument,
                SourceStreamId = "source-stream",
                TargetStreamId = "target-stream",
                TargetDocument = targetDocument,
                DataStore = dataStore,
                DocumentStore = documentStore,
                Options = options,
                Transformer = transformer
            };

            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success, $"Expected success but got error: {result.Error}");
            // Should have 2 events (Event0 and LateEvent)
            Assert.Equal(2, result.TotalEventsCopied);
            // Verify transformer was called for both events
            await transformer.Received(2).TransformAsync(
                Arg.Any<IEvent>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class ConcurrentWriteScenarios
    {
        [Fact]
        public async Task Should_retry_when_source_version_changes_during_close()
        {
            // Arrange - simulate new events arriving during close attempt.
            // The executor reads source in CatchUpAsync, then re-reads in AttemptCloseAsync.
            // On the first iteration, we make the second read show a higher version (new event arrived).
            // On the second iteration, version stays stable so close succeeds.
            var sourceReadCount = 0;
            var targetVersion = -1;
            var firstCloseAttemptDone = false;

            var dataStore = Substitute.For<IDataStore>();

            // Track appended events to target
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "target-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(callInfo =>
                {
                    var events = callInfo.Arg<IEvent[]>();
                    targetVersion = events.Max(e => e.EventVersion);
                    return Task.CompletedTask;
                });

            // Source append (close event) - succeeds (version check happens before this)
            dataStore.AppendAsync(
                    Arg.Is<IObjectDocument>(d => d.Active.StreamIdentifier == "source-stream"),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IEvent[]>())
                .Returns(Task.CompletedTask);

            dataStore.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(callInfo =>
                {
                    var doc = callInfo.Arg<IObjectDocument>();

                    if (doc.Active.StreamIdentifier == "source-stream")
                    {
                        sourceReadCount++;

                        // Flow: Read 1 (CatchUp) -> Read 2 (AttemptClose verify) -> Read 3 (CatchUp iter 2) -> Read 4 (AttemptClose verify)
                        // On read 2, return version 1 (new event) to cause conflict
                        // After that, reads return consistent version so second close succeeds
                        int version;
                        if (sourceReadCount == 2 && !firstCloseAttemptDone)
                        {
                            // This is the re-read in AttemptCloseAsync for first iteration
                            // Return higher version to simulate new event
                            version = 1;
                            firstCloseAttemptDone = true;
                        }
                        else if (sourceReadCount <= 2)
                        {
                            version = 0;
                        }
                        else
                        {
                            // After first conflict, return stable version
                            version = 1;
                        }

                        var events = Enumerable.Range(0, version + 1)
                            .Select(i => CreateEvent($"Event{i}", i))
                            .ToList();
                        return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)events);
                    }

                    // Target stream
                    if (targetVersion < 0)
                        return Task.FromResult<IEnumerable<IEvent>?>(null);

                    var targetEvents = Enumerable.Range(0, targetVersion + 1)
                        .Select(i => CreateEvent($"Event{i}", i))
                        .ToList();
                    return Task.FromResult<IEnumerable<IEvent>?>((IEnumerable<IEvent>)targetEvents);
                });

            var documentStore = Substitute.For<IDocumentStore>();

            var options = new LiveMigrationOptions();
            options.WithCatchUpDelay(TimeSpan.Zero);

            var context = CreateContext(dataStore, documentStore, options);
            var sut = new LiveMigrationExecutor(context, CreateLoggerFactory());

            // Act
            var result = await sut.ExecuteAsync();

            // Assert
            Assert.True(result.Success, $"Expected success but got error: {result.Error}");
            Assert.True(result.Iterations >= 2, $"Expected at least 2 iterations, got {result.Iterations}");
        }
    }
}
