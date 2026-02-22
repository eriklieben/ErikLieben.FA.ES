using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Tests.Action;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.EventStream
{
    public class LeasedSessionTests
    {
        private static SessionDependencies CreateDependencies()
        {
            var eventStream = Substitute.For<IEventStream>();
            var eventTypeRegistry = new EventTypeRegistry();
            eventStream.EventTypeRegistry.Returns(eventTypeRegistry);
            var document = Substitute.For<IObjectDocument>();
            var active = Substitute.For<StreamInformation>();
            // active.ChunkSettings.Returns(Substitute.For<StreamChunkSettings>());
            // active.StreamChunks.Returns(Substitute.For<List<StreamChunk>>());
            document.Active.Returns(active);

            var dataStore = Substitute.For<IDataStore, IDataStoreRecovery>();
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventNames = Substitute.For<Dictionary<Type, string>>();

            var docClosedNotificationActions = new List<IStreamDocumentChunkClosedNotification>();
            var postCommitActions = new List<IAsyncPostCommitAction>();
            var preAppendActions = new List<IPreAppendAction>();
            var postReadActions = new List<IPostReadAction>();

            document.TerminatedStreams.Returns([]);

            return new SessionDependencies
            {
                EventStream = eventStream,
                Document = document,
                DataStore = dataStore,
                DocumentFactory = documentFactory,
                EventNames = eventNames,
                DocClosedNotificationActions = docClosedNotificationActions,
                PostCommitActions = postCommitActions,
                PreAppendActions = preAppendActions,
                PostReadActions = postReadActions,
                EventTypeRegistry = eventTypeRegistry
            };
        }

        private static LeasedSession CreateSut(SessionDependencies dependencies)
        {
            return new LeasedSession(
                dependencies.EventStream,
                dependencies.Document,
                dependencies.DataStore,
                dependencies.DocumentFactory,
                dependencies.DocClosedNotificationActions,
                dependencies.PostCommitActions,
                dependencies.PreAppendActions,
                dependencies.PostReadActions);
        }

        public class Ctor
        {
            [Fact]
            public void Should_initialize_correctly_with_valid_parameters()
            {
                // Arrange
                var dependencies = CreateDependencies();

                // Act
                var sut = new LeasedSession(
                    dependencies.EventStream,
                    dependencies.Document,
                    dependencies.DataStore,
                    dependencies.DocumentFactory,
                    dependencies.DocClosedNotificationActions,
                    dependencies.PostCommitActions,
                    dependencies.PreAppendActions,
                    dependencies.PostReadActions);

                // Assert
                Assert.NotNull(sut);
                Assert.Empty(sut.Buffer);
            }

            [Fact]
            public void Should_throw_when_EventStream_is_null()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.EventStream = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new LeasedSession(
                    dependencies.EventStream,
                    dependencies.Document,
                    dependencies.DataStore,
                    dependencies.DocumentFactory,
                    dependencies.DocClosedNotificationActions,
                    dependencies.PostCommitActions,
                    dependencies.PreAppendActions,
                    dependencies.PostReadActions));
            }

            [Fact]
            public void Should_throw_when_document_is_null()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new LeasedSession(
                    dependencies.EventStream,
                    dependencies.Document,
                    dependencies.DataStore,
                    dependencies.DocumentFactory,
                    dependencies.DocClosedNotificationActions,
                    dependencies.PostCommitActions,
                    dependencies.PreAppendActions,
                    dependencies.PostReadActions));
            }

            [Fact]
            public void Should_throw_when_active_document_is_null()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.Returns((StreamInformation)null!);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new LeasedSession(
                    dependencies.EventStream,
                    dependencies.Document,
                    dependencies.DataStore,
                    dependencies.DocumentFactory,
                    dependencies.DocClosedNotificationActions,
                    dependencies.PostCommitActions,
                    dependencies.PreAppendActions,
                    dependencies.PostReadActions));
            }

            [Fact]
            public void Should_throw_when_datastore_is_null()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.DataStore = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new LeasedSession(
                    dependencies.EventStream,
                    dependencies.Document,
                    dependencies.DataStore,
                    dependencies.DocumentFactory,
                    dependencies.DocClosedNotificationActions,
                    dependencies.PostCommitActions,
                    dependencies.PreAppendActions,
                    dependencies.PostReadActions));
            }

            [Fact]
            public void Should_throw_when_DocumentStore_is_null()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.DocumentFactory = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new LeasedSession(
                    dependencies.EventStream,
                    dependencies.Document,
                    dependencies.DataStore,
                    dependencies.DocumentFactory,
                    dependencies.DocClosedNotificationActions,
                    dependencies.PostCommitActions,
                    dependencies.PreAppendActions,
                    dependencies.PostReadActions));
            }

            [Fact]
            public void Should_throw_when_DocClosedNotificationActions_is_null()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.DocClosedNotificationActions = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new LeasedSession(
                    dependencies.EventStream,
                    dependencies.Document,
                    dependencies.DataStore,
                    dependencies.DocumentFactory,
                    dependencies.DocClosedNotificationActions,
                    dependencies.PostCommitActions,
                    dependencies.PreAppendActions,
                    dependencies.PostReadActions));
            }

            [Fact]
            public void Should_throw_when_PostCommitActions_is_null()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.PostCommitActions = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new LeasedSession(
                    dependencies.EventStream,
                    dependencies.Document,
                    dependencies.DataStore,
                    dependencies.DocumentFactory,
                    dependencies.DocClosedNotificationActions,
                    dependencies.PostCommitActions,
                    dependencies.PreAppendActions,
                    dependencies.PostReadActions));
            }
        }

        public class Append
        {
            [Fact]
            public void Should_throw_when_payload_is_null()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                TestPayload payload = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.Append(payload));
            }

            [Fact]
            public void Should_append_event_to_buffer()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var payload = new TestPayload { Id = 1, Name = "Test" };
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);

                // Act
                var result = sut.Append(payload);

                // Assert
                Assert.Single(sut.Buffer);
                Assert.Equal("TestEvent", sut.Buffer[0].EventType);
                Assert.Equal(0, sut.Buffer[0].EventVersion);
                Assert.Equal("{\"Id\":1,\"Name\":\"Test\"}", sut.Buffer[0].Payload);
                Assert.NotNull(result);
            }

            [Fact]
            public void Should_use_override_event_type_when_provided()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);
                var sut = CreateSut(dependencies);
                var payload = new TestPayload { Id = 1, Name = "Test" };
                var overrideEventType = "OverrideEvent";

                // Act
                var result = sut.Append(payload, overrideEventType: overrideEventType);

                // Assert
                Assert.Single(sut.Buffer);
                Assert.Equal(overrideEventType, sut.Buffer[0].EventType);
            }

            [Fact]
            public void Should_use_provided_action_metadata()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var payload = new TestPayload { Id = 1, Name = "Test" };
                var actionMetadata = new ActionMetadata { CorrelationId = "123" };
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);

                // Act
                var result = sut.Append(payload, actionMetadata: actionMetadata);

                // Assert
                Assert.Single(sut.Buffer);
                Assert.Equal(actionMetadata, sut.Buffer[0].ActionMetadata);
            }

            [Fact]
            public void Should_use_provided_external_sequencer()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var payload = new TestPayload { Id = 1, Name = "Test" };
                var externalSequencer = "seq123";
                dependencies.EventNames.Add(typeof(TestPayload), "TestEvent");
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);

                // Act
                var result = sut.Append(payload, externalSequencer: externalSequencer);

                // Assert
                Assert.Single(sut.Buffer);
                Assert.Equal(externalSequencer, sut.Buffer[0].ExternalSequencer);
            }

            [Fact]
            public void Should_use_provided_metadata()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var payload = new TestPayload { Id = 1, Name = "Test" };
                var metadata = new Dictionary<string, string> { { "key1", "value1" } };
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);

                // Act
                var result = sut.Append(payload, metadata: metadata);

                // Assert
                Assert.Single(sut.Buffer);
                Assert.Equal(metadata, sut.Buffer[0].Metadata);
            }

            [Fact]
            public void Should_execute_pre_append_actions()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var preAppendAction = Substitute.For<IPreAppendAction>();
                dependencies.PreAppendActions = [preAppendAction];

                var sut = CreateSut(dependencies);
                var payload = new TestPayload { Id = 1, Name = "Test" };
                var activeStream = Substitute.For<StreamInformation>();
                activeStream.CurrentStreamVersion = 0;
                dependencies.Document.Active.Returns(activeStream);
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);

                var transformedPayload = new TestPayload { Id = 2, Name = "Transformed" };
                preAppendAction.PreAppend(Arg.Any<TestPayload>(), Arg.Any<JsonEvent>(), Arg.Any<IObjectDocument>())
                    .Returns(_ => () => transformedPayload);

                // Act
                _ = sut.Append(payload);

                // Assert
                Assert.Single(sut.Buffer);
                Assert.Equal("{\"Id\":2,\"Name\":\"Transformed\"}", sut.Buffer[0].Payload);
                preAppendAction.Received(1).PreAppend(Arg.Any<TestPayload>(), Arg.Any<JsonEvent>(), Arg.Any<IObjectDocument>());
            }

            [Fact]
            public void Should_pass_original_payload_to_pre_append_actions()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var preAppendAction = Substitute.For<IPreAppendAction>();
                dependencies.PreAppendActions = [preAppendAction];

                var sut = CreateSut(dependencies);
                var originalPayload = new TestPayload { Id = 1, Name = "Original" };
                var activeStream = Substitute.For<StreamInformation>();
                activeStream.CurrentStreamVersion = 0;
                dependencies.Document.Active.Returns(activeStream);
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);

                TestPayload? receivedPayload = null;
                preAppendAction.PreAppend(Arg.Any<TestPayload>(), Arg.Any<JsonEvent>(), Arg.Any<IObjectDocument>())
                    .Returns(callInfo =>
                    {
                        receivedPayload = callInfo.ArgAt<TestPayload>(0);
                        return (Func<TestPayload>)(() => receivedPayload);
                    });

                // Act
                sut.Append(originalPayload);

                // Assert - pre-append action should receive the original payload object
                Assert.NotNull(receivedPayload);
                Assert.Equal(1, receivedPayload!.Id);
                Assert.Equal("Original", receivedPayload.Name);
            }

            [Fact]
            public void Should_serialize_only_once_after_all_pre_append_actions()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var preAppendAction1 = Substitute.For<IPreAppendAction>();
                var preAppendAction2 = Substitute.For<IPreAppendAction>();
                dependencies.PreAppendActions = [preAppendAction1, preAppendAction2];

                var sut = CreateSut(dependencies);
                var originalPayload = new TestPayload { Id = 1, Name = "Original" };
                var activeStream = Substitute.For<StreamInformation>();
                activeStream.CurrentStreamVersion = 0;
                dependencies.Document.Active.Returns(activeStream);
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);

                // First action transforms the payload
                var intermediatePayload = new TestPayload { Id = 2, Name = "Intermediate" };
                preAppendAction1.PreAppend(Arg.Any<TestPayload>(), Arg.Any<JsonEvent>(), Arg.Any<IObjectDocument>())
                    .Returns(_ => (Func<TestPayload>)(() => intermediatePayload));

                // Second action transforms further - receives the intermediate result
                var finalPayload = new TestPayload { Id = 3, Name = "Final" };
                preAppendAction2.PreAppend(Arg.Any<TestPayload>(), Arg.Any<JsonEvent>(), Arg.Any<IObjectDocument>())
                    .Returns(_ => (Func<TestPayload>)(() => finalPayload));

                // Act
                sut.Append(originalPayload);

                // Assert - the buffer should contain the serialization of the final payload
                Assert.Single(sut.Buffer);
                Assert.Equal("{\"Id\":3,\"Name\":\"Final\"}", sut.Buffer[0].Payload);
            }

            [Fact]
            public void Should_set_schema_version_from_registry()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var payload = new TestPayload { Id = 1, Name = "Test" };

                // Register with schema version 2
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", 2, TestJsonSerializerContext.Default.TestPayload);

                // Act
                var result = sut.Append(payload);

                // Assert
                Assert.Single(sut.Buffer);
                Assert.Equal(2, sut.Buffer[0].SchemaVersion);
            }

            [Fact]
            public void Should_default_to_schema_version_1_when_not_specified()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var payload = new TestPayload { Id = 1, Name = "Test" };

                // Register without explicit schema version (defaults to 1)
                dependencies.EventTypeRegistry.Add(typeof(TestPayload), "TestEvent", TestJsonSerializerContext.Default.TestPayload);

                // Act
                var result = sut.Append(payload);

                // Assert
                Assert.Single(sut.Buffer);
                Assert.Equal(1, sut.Buffer[0].SchemaVersion);
            }
        }

        public class CommitAsync
        {
            [Fact]
            public async Task Should_commit_events_when_not_chunking_is_set_an_empty_buffer()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = false
                };

                var sut = CreateSut(dependencies);
                var event1 = new JsonEvent { EventType = "TestEvent", EventVersion = 1 };
                var event2 = new JsonEvent { EventType = "TestEvent", EventVersion = 2 };
                sut.Buffer.Add(event1);
                sut.Buffer.Add(event2);

                // Act
                await sut.CommitAsync();

                // Assert
                await dependencies.DocumentFactory.Received(1).SetAsync(dependencies.Document);
                await dependencies.DataStore.Received(1)
                    .AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Is<IEvent[]>(e => e.Length == 2));
                ;
                Assert.Empty(sut.Buffer);
            }

            [Fact]
            public async Task Should_execute_post_commit_actions()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = false
                };
                var postCommitAction = Substitute.For<IAsyncPostCommitAction>();
                dependencies.PostCommitActions = [postCommitAction];

                var sut = CreateSut(dependencies);
                var @event = new JsonEvent { EventType = "TestEvent", EventVersion = 1 };
                sut.Buffer.Add(@event);

                // Act
                await sut.CommitAsync();

                // Assert
                await postCommitAction.Received(1).PostCommitAsync(
                    Arg.Is<List<JsonEvent>>(e => e.Count == 1),
                    dependencies.Document);
            }

            [Fact]
            public async Task Should_handle_chunking_with_empty_stream_chunks_collection()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 0 });
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    ChunkSize = 10,
                    EnableChunks = true
                };
                dependencies.Document.Active.StreamChunks = [];

                // Act
                await sut.CommitAsync();

                // Assert
                await dependencies.DocumentFactory.Received(2).SetAsync(dependencies.Document);
                await dependencies.DataStore.Received(1).AppendAsync(dependencies.Document, Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>());
            }

            [Fact]
            public async Task Should_handle_chunking_with_existing_stream_chunks()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);

                var event1 = new JsonEvent { EventType = "TestEvent", EventVersion = 1 };
                sut.Buffer.Add(event1);

                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true,
                    ChunkSize = 10
                };
                var streamChunk = new StreamChunk { ChunkIdentifier = 1 };
                dependencies.Document.Active.StreamChunks = [streamChunk];

                // Act
                await sut.CommitAsync();

                // Assert
                Assert.Equal(1, streamChunk.LastEventVersion);
                await dependencies.DocumentFactory.Received(1).SetAsync(dependencies.Document);
                await dependencies.DataStore.Received(1).AppendAsync(dependencies.Document, Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>());
            }

            [Fact]
            public async Task Should_create_new_chunk_when_buffer_exceeds_available_space()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var notification = Substitute.For<IStreamDocumentChunkClosedNotification>();
                var called = false;
                notification.StreamDocumentChunkClosed().Returns(_ => (eventStream, chunkId) =>
                {
                    called = true;
                    return Task.CompletedTask;
                });
                dependencies.DocClosedNotificationActions = [notification];

                var sut = CreateSut(dependencies);
                for (var i = 1; i <= 15; i++)
                {
                    sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = i });
                }

                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true,
                    ChunkSize = 1000
                };
                var streamChunk = new StreamChunk
                    { ChunkIdentifier = 1, FirstEventVersion = 0, LastEventVersion = 990 };
                dependencies.Document.Active.StreamChunks.Add(streamChunk);

                // Act
                await sut.CommitAsync();

                // Assert
                var lastChunk = dependencies.Document.Active.StreamChunks.Last();
                Assert.Equal(2, lastChunk.ChunkIdentifier);
                Assert.Empty(sut.Buffer);
                Assert.True(called);
            }

            [Fact]
            public async Task Should_create_new_chunk_when_out_of_available_space_when_done()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var notification = Substitute.For<IStreamDocumentChunkClosedNotification>();
                notification.StreamDocumentChunkClosed().Returns(Substitute.For<Func<IEventStream, int, Task>>());
                dependencies.DocClosedNotificationActions = [notification];
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 999 });
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true,
                    ChunkSize = 1000
                };
                // The first chunk contains 1000 items, 0 to 999, so 998 has space for one item
                var streamChunk = new StreamChunk
                {
                    ChunkIdentifier = 1,
                    LastEventVersion = 998
                };
                dependencies.Document.Active.StreamChunks.Add(streamChunk);

                // Act
                await sut.CommitAsync();

                // Assert
                var lastChunk = dependencies.Document.Active.StreamChunks.Last();
                Assert.Equal(2, lastChunk.ChunkIdentifier);
                await notification.StreamDocumentChunkClosed().Received(1)(Arg.Any<IEventStream>(), 1);
            }

            [Fact]
            public async Task Should_throw_CommitFailedException_when_document_update_fails()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.StreamIdentifier = "test-stream";
                dependencies.Document.Active.CurrentStreamVersion = 5;
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 6 });

                var innerException = new InvalidOperationException("Document update failed");
                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(innerException));

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert
                Assert.Equal("test-stream", exception.StreamIdentifier);
                Assert.Equal(4, exception.OriginalVersion); // 5 - 1 buffered event
                Assert.Equal(5, exception.AttemptedVersion);
                Assert.False(exception.EventsMayBeWritten); // Document failed before events
                Assert.Same(innerException, exception.InnerException);
            }

            [Fact]
            public async Task Should_throw_CommitFailedException_when_event_append_fails()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.StreamIdentifier = "test-stream";
                dependencies.Document.Active.CurrentStreamVersion = 5;
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 6 });

                var innerException = new InvalidOperationException("Event append failed");
                dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
                    .Returns(Task.FromException(innerException));

                // Cleanup succeeds (returns removed count)
                ((IDataStoreRecovery)dependencies.DataStore)
                    .RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
                    .Returns(1);

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert
                Assert.Equal("test-stream", exception.StreamIdentifier);
                Assert.Equal(4, exception.OriginalVersion);
                Assert.Equal(5, exception.AttemptedVersion);
                // After successful cleanup, EventsMayBeWritten is false (cleaned up, safe to retry)
                Assert.False(exception.EventsMayBeWritten);
                Assert.Same(innerException, exception.InnerException);
            }

            [Fact]
            public async Task Should_restore_version_on_commit_failure()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.CurrentStreamVersion = 10;
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 11 });
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 12 });

                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(new InvalidOperationException("Failed")));

                // Act
                await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert - version should be restored to original (10 - 2 events = 8)
                Assert.Equal(8, dependencies.Document.Active.CurrentStreamVersion);
            }

            [Fact]
            public async Task Should_preserve_buffer_on_commit_failure_for_retry()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var event1 = new JsonEvent { EventType = "TestEvent", EventVersion = 1 };
                var event2 = new JsonEvent { EventType = "TestEvent", EventVersion = 2 };
                sut.Buffer.Add(event1);
                sut.Buffer.Add(event2);

                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(new InvalidOperationException("Failed")));

                // Act
                await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert - buffer should be preserved for retry
                Assert.Equal(2, sut.Buffer.Count);
                Assert.Contains(event1, sut.Buffer);
                Assert.Contains(event2, sut.Buffer);
            }

            [Fact]
            public async Task Should_call_document_store_before_data_store()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings { EnableChunks = false };
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 1 });

                var callOrder = new List<string>();
                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.CompletedTask)
                    .AndDoes(_ => callOrder.Add("DocumentStore"));
                dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
                    .Returns(Task.CompletedTask)
                    .AndDoes(_ => callOrder.Add("DataStore"));

                // Act
                await sut.CommitAsync();

                // Assert - document store should be called first for optimistic concurrency
                Assert.Equal(2, callOrder.Count);
                Assert.Equal("DocumentStore", callOrder[0]);
                Assert.Equal("DataStore", callOrder[1]);
            }

            [Fact]
            public async Task Should_indicate_safe_retry_when_document_fails_before_events()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 1 });

                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(new InvalidOperationException("Concurrency conflict")));

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert
                Assert.False(exception.EventsMayBeWritten);
                Assert.Contains("Safe to retry", exception.Message);
            }

            [Fact]
            public async Task Should_indicate_cleanup_succeeded_when_append_fails_and_cleanup_works()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 1 });

                dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
                    .Returns(Task.FromException(new InvalidOperationException("Partial write")));

                // Cleanup succeeds
                ((IDataStoreRecovery)dependencies.DataStore)
                    .RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
                    .Returns(1);

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert - after successful cleanup, EventsMayBeWritten is false
                Assert.False(exception.EventsMayBeWritten);
                Assert.Contains("Safe to retry", exception.Message);
            }

            [Fact]
            public async Task Should_handle_chunking_document_failure_before_events()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.StreamIdentifier = "chunked-stream";
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true,
                    ChunkSize = 10
                };
                dependencies.Document.Active.StreamChunks = [new StreamChunk { ChunkIdentifier = 1 }];

                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 1 });

                var innerException = new InvalidOperationException("Chunking document update failed");
                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(innerException));

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert
                Assert.False(exception.EventsMayBeWritten);
                Assert.Equal("chunked-stream", exception.StreamIdentifier);
            }

            [Fact]
            public async Task Should_handle_chunking_event_append_failure_after_document_update()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.StreamIdentifier = "chunked-stream";
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true,
                    ChunkSize = 10
                };
                dependencies.Document.Active.StreamChunks = [new StreamChunk { ChunkIdentifier = 1 }];

                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 1 });

                var innerException = new InvalidOperationException("Chunking event append failed");
                dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
                    .Returns(Task.FromException(innerException));

                // Cleanup succeeds
                ((IDataStoreRecovery)dependencies.DataStore)
                    .RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
                    .Returns(1);

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert - after successful cleanup, EventsMayBeWritten is false
                Assert.False(exception.EventsMayBeWritten);
                Assert.Equal("chunked-stream", exception.StreamIdentifier);
            }

            [Fact]
            public async Task Should_correctly_calculate_version_delta_with_multiple_events()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.StreamIdentifier = "test-stream";
                dependencies.Document.Active.CurrentStreamVersion = 100;
                var sut = CreateSut(dependencies);

                // Add 5 events
                for (int i = 0; i < 5; i++)
                {
                    sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 101 + i });
                }

                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(new InvalidOperationException("Failed")));

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert
                Assert.Equal(95, exception.OriginalVersion); // 100 - 5 events
                Assert.Equal(100, exception.AttemptedVersion);
            }

            [Fact]
            public async Task Should_not_execute_post_commit_actions_on_failure()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var postCommitAction = Substitute.For<IAsyncPostCommitAction>();
                dependencies.PostCommitActions = [postCommitAction];
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 1 });

                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(new InvalidOperationException("Failed")));

                // Act
                await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert - post commit action should NOT be called
                await postCommitAction.DidNotReceive().PostCommitAsync(
                    Arg.Any<List<JsonEvent>>(),
                    Arg.Any<IObjectDocument>());
            }

            [Fact]
            public async Task Should_clear_buffer_only_on_success()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 1 });

                // First call fails
                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(new InvalidOperationException("Failed")));

                // Act - first attempt fails
                await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());
                Assert.Single(sut.Buffer); // Buffer preserved

                // Configure success for retry
                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.CompletedTask);

                // Act - retry succeeds
                await sut.CommitAsync();

                // Assert - buffer cleared on success
                Assert.Empty(sut.Buffer);
            }

            [Fact]
            public async Task Should_have_correct_error_code_in_exception()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = 1 });

                dependencies.DocumentFactory.SetAsync(Arg.Any<IObjectDocument>())
                    .Returns(Task.FromException(new InvalidOperationException("Failed")));

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert
                Assert.Equal("ELFAES-COMMIT-0001", exception.ErrorCode);
            }

            [Fact]
            public async Task Should_track_events_started_across_multiple_chunk_batches()
            {
                // Arrange - set up scenario where first batch succeeds but second fails
                var dependencies = CreateDependencies();
                dependencies.Document.Active.StreamIdentifier = "multi-chunk-stream";
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true,
                    ChunkSize = 5 // Small chunk size to force multiple batches
                };
                // Start with chunk at position 3 (2 more slots available)
                dependencies.Document.Active.StreamChunks = [
                    new StreamChunk { ChunkIdentifier = 1, FirstEventVersion = 1, LastEventVersion = 3 }
                ];

                var sut = CreateSut(dependencies);
                // Add more events than fit in current chunk
                for (int i = 4; i <= 8; i++)
                {
                    sut.Buffer.Add(new JsonEvent { EventType = "TestEvent", EventVersion = i });
                }

                var callCount = 0;
                dependencies.DataStore.AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Any<IEvent[]>())
                    .Returns(_ =>
                    {
                        callCount++;
                        if (callCount == 1)
                        {
                            return Task.CompletedTask; // First batch succeeds
                        }
                        return Task.FromException(new InvalidOperationException("Second batch failed"));
                    });

                // Cleanup succeeds
                ((IDataStoreRecovery)dependencies.DataStore)
                    .RemoveEventsForFailedCommitAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int>())
                    .Returns(5);

                // Act
                var exception = await Assert.ThrowsAsync<CommitFailedException>(() => sut.CommitAsync());

                // Assert - after successful cleanup, EventsMayBeWritten is false
                Assert.False(exception.EventsMayBeWritten);
            }

            [Fact]
            public async Task Should_handle_empty_buffer_commit_gracefully()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.Document.Active.ChunkSettings = new StreamChunkSettings { EnableChunks = false };
                var sut = CreateSut(dependencies);
                // No events in buffer

                // Act
                await sut.CommitAsync();

                // Assert - should complete without calling stores
                await dependencies.DocumentFactory.Received(1).SetAsync(Arg.Any<IObjectDocument>());
                await dependencies.DataStore.Received(1).AppendAsync(Arg.Any<IObjectDocument>(), Arg.Any<CancellationToken>(), Arg.Is<IEvent[]>(e => e.Length == 0));
            }

        }

        public class IsTerminatedAsync
        {
            [Fact]
            public async Task Should_return_true_when_stream_is_terminated()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var streamIdentifier = "test-stream";

                dependencies.Document.TerminatedStreams.Returns([new() { StreamIdentifier = streamIdentifier }]);

                // Act
                var result = await sut.IsTerminatedAsync(streamIdentifier);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public async Task Should_return_false_when_stream_is_not_terminated()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var streamIdentifier = "test-stream";

                dependencies.Document.TerminatedStreams.Returns([]);

                // Act
                var result = await sut.IsTerminatedAsync(streamIdentifier);

                // Assert
                Assert.False(result);
            }


            [Fact]
            public async Task Should_check_if_stream_is_terminated_when_it_is()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var streamId = "stream-123";
                dependencies.Document.TerminatedStreams.Returns([new TerminatedStream { StreamIdentifier = streamId }]);
                var sut = CreateSut(dependencies);

                // Act
                var result = await sut.IsTerminatedAsync(streamId);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public async Task Should_check_if_stream_is_terminated_when_it_is_not()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var streamId = "stream-123";
                dependencies.Document.TerminatedStreams.Returns([
                    new TerminatedStream { StreamIdentifier = "other-stream" }
                ]);
                var sut = CreateSut(dependencies);

                // Act
                var result = await sut.IsTerminatedAsync(streamId);

                // Assert
                Assert.False(result);
            }
        }

        public class ReadAsync {

            [Fact]
            public async Task Should_read_events_with_default_parameters()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var expectedEvents = new List<IEvent>
                {
                    Substitute.For<IEvent>(),
                    Substitute.For<IEvent>()
                };

                dependencies.DataStore.ReadAsync(dependencies.Document, 0, null)
                    .Returns(Task.FromResult<IEnumerable<IEvent>?>(expectedEvents));

                // Act
                var result = await sut.ReadAsync();

                // Assert
                Assert.Equal(expectedEvents, result);
            }

            [Fact]
            public async Task Should_read_events_with_custom_start_version()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                const int startVersion = 5;

                // Act
                await sut.ReadAsync(startVersion);

                // Assert
                await dependencies.DataStore.Received(1).ReadAsync(dependencies.Document, startVersion, null);
            }

            [Fact]
            public async Task Should_read_events_with_until_version()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var sut = CreateSut(dependencies);
                var startVersion = 5;
                var untilVersion = 10;

                // Act
                await sut.ReadAsync(startVersion, untilVersion);

                // Assert
                await dependencies.DataStore.Received(1).ReadAsync(dependencies.Document, startVersion, untilVersion);
            }

            [Fact]
            public async Task Should_read_events_with_specific_parameters()
            {
                // Arrange
                var dependencies = CreateDependencies();
                var events = new List<IEvent> { Substitute.For<IEvent>() };
                dependencies.DataStore
                    .ReadAsync(dependencies.Document, 10, 20)
                    .Returns(Task.FromResult<IEnumerable<IEvent>?>(events));

                var sut = CreateSut(dependencies);

                // Act
                var result = await sut.ReadAsync(10, 20);

                // Assert
                Assert.Equal(events, result);
                await dependencies.DataStore.Received(1).ReadAsync(dependencies.Document, 10, 20);
            }

            [Fact]
            public async Task Should_read_events_with_null_result()
            {
                // Arrange
                var dependencies = CreateDependencies();
                dependencies.DataStore
                    .ReadAsync(dependencies.Document, 0, null)
                    .Returns(Task.FromResult<IEnumerable<IEvent>?>(null));

                var sut = CreateSut(dependencies);

                // Act
                var result = await sut.ReadAsync();

                // Assert
                Assert.Null(result);
            }
        }

        private class SessionDependencies
        {
            public IEventStream EventStream { get; set; } = null!;
            public IObjectDocument Document { get; set; } = null!;
            public IDataStore DataStore { get; set; } = null!;
            public IObjectDocumentFactory DocumentFactory { get; set; } = null!;
            public Dictionary<Type, string> EventNames { get; set; } = null!;
            public List<IStreamDocumentChunkClosedNotification> DocClosedNotificationActions { get; set; } = null!;
            public List<IAsyncPostCommitAction> PostCommitActions { get; set; } = null!;
            public List<IPreAppendAction> PreAppendActions { get; set; } = null!;
            public List<IPostReadAction> PostReadActions { get; set; } = null!;

            public EventTypeRegistry EventTypeRegistry { get; set; } = null!;
        }



    }

    internal class TestPayload
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [JsonSerializable(typeof(TestPayload))]
    internal partial class TestJsonSerializerContext : JsonSerializerContext { }
}
