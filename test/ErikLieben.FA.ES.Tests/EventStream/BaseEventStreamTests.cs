using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Upcasting;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.EventStream
{
    public class BaseEventStreamTests
    {
        public class Ctor
        {
            [Fact]
            public void Should_throw_exception_when_document_is_null()
            {
                // Arrange
                IObjectDocumentWithMethods? document = null;
                var dependencies = Substitute.For<IStreamDependencies>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new TestEventStream(document!, dependencies));
            }

            [Fact]
            public void Should_throw_exception_when_stream_dependencies_is_null()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                IStreamDependencies? dependencies = null;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new TestEventStream(document, dependencies!));
            }

            [Fact]
            public void Should_initialize_properties_correctly()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();

                // Act
                var sut = new TestEventStream(document, dependencies);

                // Assert
                Assert.Equal(document, sut.Document);
                Assert.Equal(dependencies, sut.StreamDependencies);
                Assert.NotNull(sut.Settings);
                Assert.Empty(sut.GetActions());
                Assert.Empty(sut.GetNotifications());
                Assert.Empty(sut.GetUpcasters());
                Assert.Null(sut.GetJsonTypeInfoSnapshot());
                Assert.Null(sut.GetJsonTypeInfoAgg());
            }
        }

        public class ReadAsync
        {
            [Fact]
            public async Task Should_read_events_with_chunking_enabled()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true
                };

                var chunk1 = new StreamChunk { ChunkIdentifier = 1 };
                var chunk2 = new StreamChunk { ChunkIdentifier = 2 };
                active.StreamChunks = [chunk1, chunk2];

                var dependencies = Substitute.For<IStreamDependencies>();
                var events1 = new List<IEvent> { Substitute.For<IEvent>(), Substitute.For<IEvent>() };
                var events2 = new List<IEvent> { Substitute.For<IEvent>() };

                dependencies.DataStore.ReadAsync(document, 0, null, chunk: 1).Returns(events1);
                dependencies.DataStore.ReadAsync(document, 0, null, chunk: 2).Returns(events2);

                var sut = new TestEventStream(document, dependencies);

                // Act
                var result = await sut.ReadAsync();

                // Assert
                Assert.Equal(3, result.Count);
                await dependencies.DataStore.Received(1).ReadAsync(document, 0, null, chunk: 1);
                await dependencies.DataStore.Received(1).ReadAsync(document, 0, null, chunk: 2);
            }

            [Fact]
            public async Task Should_read_events_without_chunking()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = false
                };

                var dependencies = Substitute.For<IStreamDependencies>();
                var events = new List<IEvent> { Substitute.For<IEvent>(), Substitute.For<IEvent>() };

                dependencies.DataStore.ReadAsync(document, 0, null, chunk: null).Returns(events);

                var sut = new TestEventStream(document, dependencies);

                // Act
                var result = await sut.ReadAsync();

                // Assert
                Assert.Equal(2, result.Count);
                await dependencies.DataStore.Received(1).ReadAsync(document, 0, null, chunk: null);
            }

            [Fact]
            public async Task Should_order_by_external_sequencer_when_specified()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = false
                };

                var dependencies = Substitute.For<IStreamDependencies>();

                var event1 = Substitute.For<JsonEvent>();
                event1.ExternalSequencer = "2";
                var event2 = Substitute.For<JsonEvent>();
                event2.ExternalSequencer = "1";
                var events = new List<IEvent> { event1, event2 };
                dependencies.DataStore.ReadAsync(document, 0, null, chunk: null).Returns(events);
                var sut = new TestEventStream(document, dependencies);

                // Act
                var result = (await sut.ReadAsync(useExternalSequencer: true)).ToList();

                // Assert
                Assert.Equal(2, result.Count);
                Assert.Equal("1", result[0].ExternalSequencer);
                Assert.Equal("2", result[1].ExternalSequencer);
            }

            [Fact]
            public async Task Should_apply_upcasting_when_UpCasters_are_registered()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = false
                };

                var dependencies = Substitute.For<IStreamDependencies>();
                var originalEvent = Substitute.For<IEvent>();
                var upcastedEvent = Substitute.For<IEvent>();

                var events = new List<IEvent> { originalEvent };
                dependencies.DataStore.ReadAsync(document, 0, null, chunk: null).Returns(events);

                var upCaster = Substitute.For<IEventUpcaster>();
                upCaster.CanUpcast(originalEvent).Returns(true);
                upCaster.UpCast(originalEvent).Returns(new[] { upcastedEvent });

                var sut = new TestEventStream(document, dependencies);
                sut.RegisterUpcaster(upCaster);

                // Act
                var result = await sut.ReadAsync();

                // Assert
                Assert.Single(result);
                Assert.Equal(upcastedEvent, result.First());
            }

            [Fact]
            public async Task Should_handle_multiple_events_from_upcaster()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = false
                };
                var dependencies = Substitute.For<IStreamDependencies>();
                var originalEvent = Substitute.For<IEvent>();
                var upcastedEvent1 = Substitute.For<IEvent>();
                var upcastedEvent2 = Substitute.For<IEvent>();
                var events = new List<IEvent> { originalEvent };
                dependencies.DataStore.ReadAsync(document, 0, null, chunk: null).Returns(events);

                var upCaster = Substitute.For<IEventUpcaster>();
                upCaster.CanUpcast(originalEvent).Returns(true);
                upCaster.UpCast(originalEvent).Returns([upcastedEvent1, upcastedEvent2]);

                var sut = new TestEventStream(document, dependencies);
                sut.RegisterUpcaster(upCaster);

                // Act
                var result = await sut.ReadAsync();

                // Assert
                Assert.Equal(2, result.Count);
                var resultList = result.ToList();
                Assert.Contains(upcastedEvent1, resultList);
                Assert.Contains(upcastedEvent2, resultList);
            }
        }

        public class RegisterEvent
        {
            // [Fact]
            // public void Should_register_event_name()
            // {
            //     // Arrange
            //     var document = Substitute.For<IObjectDocumentWithMethods>();
            //     var dependencies = Substitute.For<IStreamDependencies>();
            //     var sut = new TestEventStream(document, dependencies);
            //
            //     // Act
            //     sut.RegisterEvent<TestEvent>("TestEventName");
            //
            //     // Assert
            //     var eventNames = sut.GetEventNames();
            //     Assert.Single(eventNames);
            //     Assert.Equal("TestEventName", eventNames[typeof(TestEvent)]);
            // }

            // [Fact]
            // public void Should_update_existing_event_name()
            // {
            //     // Arrange
            //     var document = Substitute.For<IObjectDocumentWithMethods>();
            //     var dependencies = Substitute.For<IStreamDependencies>();
            //     var sut = new TestEventStream(document, dependencies);
            //     sut.RegisterEvent<TestEvent>("OriginalName");
            //
            //     // Act
            //     sut.RegisterEvent<TestEvent>("UpdatedName");
            //
            //     // Assert
            //     var eventNames = sut.GetEventNames();
            //     Assert.Single(eventNames);
            //     Assert.Equal("UpdatedName", eventNames[typeof(TestEvent)]);
            // }
        }

        public class RegisterAction
        {
            [Fact]
            public void Should_register_action()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);
                var action = Substitute.For<IAction>();

                // Act
                sut.RegisterAction(action);

                // Assert
                Assert.Single(sut.GetActions());
                Assert.Contains(action, sut.GetActions());
            }

            [Fact]
            public void Should_throw_exception_when_registering_null_action()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.RegisterAction(null!));
            }
        }

        public class RegisterPostAppendAction
        {
            [Fact]
            public void Should_register_post_append_action()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);
                var action = Substitute.For<IPostAppendAction>();

                // Act
                sut.RegisterPostAppendAction(action);

                // Assert
                Assert.Single(sut.GetActions());
                Assert.Contains(action, sut.GetActions());
            }

            [Fact]
            public void Should_throw_exception_when_registering_null_post_append_action()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.RegisterPostAppendAction(null!));
            }
        }

        public class RegisterPostReadAction
        {
            [Fact]
            public void Should_register_post_read_action()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);
                var action = Substitute.For<IPostReadAction>();

                // Act
                sut.RegisterPostReadAction(action);

                // Assert
                Assert.Single(sut.GetActions());
                Assert.Contains(action, sut.GetActions());
            }

            [Fact]
            public void Should_throw_exception_when_registering_null_post_read_action()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.RegisterPostReadAction(null!));
            }
        }

        public class RegisterPreAppendAction
        {
            [Fact]
            public void Should_register_pre_append_action()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);
                var action = Substitute.For<IPreAppendAction>();

                // Act
                sut.RegisterPreAppendAction(action);

                // Assert
                Assert.Single(sut.GetActions());
                Assert.Contains(action, sut.GetActions());
            }

            [Fact]
            public void Should_throw_exception_when_registering_null_pre_append_action()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.RegisterPreAppendAction(null!));
            }
        }

        public class RegisterNotification
        {
            [Fact]
            public void Should_register_notification()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);
                var notification = Substitute.For<INotification>();

                // Act
                sut.RegisterNotification(notification);

                // Assert
                Assert.Single(sut.GetNotifications());
                Assert.Contains(notification, sut.GetNotifications());
            }

            [Fact]
            public void Should_throw_exception_when_registering_null_notification()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.RegisterNotification(null!));
            }

            [Fact]
            public void Should_register_upcaster()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);
                var upcaster = Substitute.For<IEventUpcaster>();

                // Act
                sut.RegisterUpcaster(upcaster);

                // Assert
                Assert.Single(sut.GetUpcasters());
                Assert.Contains(upcaster, sut.GetUpcasters());
            }

            [Fact]
            public void Should_throw_exception_when_registering_null_upcaster()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.RegisterUpcaster(null!));
            }
        }

        public class Session
        {
            [Fact]
            public async Task Should_throw_constraint_exception_when_existing_constraint_for_non_existing_stream()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true
                };
                active.CurrentStreamVersion = -1;
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns("TestId");

                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ConstraintException>(() =>
                    sut.Session(_ => { }, Constraint.Existing));

                Assert.Equal(Constraint.Existing, exception.Constraint);
            }

            [Fact]
            public async Task Should_throw_constraint_exception_when_new_constraint_for_existing_stream()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.ChunkSettings = new StreamChunkSettings
                {
                    EnableChunks = false
                };
                active.CurrentStreamVersion = 1;
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns("TestId");

                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ConstraintException>(() =>
                    sut.Session(_ => { }, Constraint.New));

                Assert.Equal(Constraint.New, exception.Constraint);
            }
        }

        public class Snapshot
        {
            [Fact]
            public async Task Should_throw_exception_when_snapshot_with_no_type_set()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                await Assert.ThrowsAsync<SnapshotJsonTypeInfoNotSetException>(() =>
                    sut.Snapshot<IBase>(1));
            }

            [Fact]
            public async Task Should_create_and_store_snapshot_successfully()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.SnapShots = [];

                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<TestAggregate>(options);

                var sut = new TestEventStream(document, dependencies);
                sut.SetAggregateType(typeInfo);

                var mockAggregate = Substitute.For<TestAggregate>();
                var typedFactory = Substitute.For<IAggregateFactory<TestAggregate>>();
                typedFactory.Create(Arg.Any<IEventStream>()).Returns(mockAggregate);
                dependencies.AggregateFactory.GetFactory<TestAggregate>().Returns(typedFactory);

                var event1 = Substitute.For<IEvent>();
                var event2 = Substitute.For<IEvent>();
                var events = new List<IEvent> { event1, event2 };
                dependencies.DataStore.ReadAsync(document, 0, 5, Arg.Any<int?>()).Returns(events);

                // Act
                await sut.Snapshot<TestAggregate>(5);

                // Assert
                dependencies.AggregateFactory.Received(1).GetFactory<TestAggregate>();
                await dependencies.SnapshotStore.Received(1).SetAsync(
                    Arg.Any<IBase>(), typeInfo, document, 5, null);

                Assert.Single(active.SnapShots);
                Assert.Equal(5, active.SnapShots[0].UntilVersion);
                Assert.Null(active.SnapShots[0].Name);

                await dependencies.ObjectDocumentFactory.Received(1).SetAsync(document);
            }

            [Fact]
            public async Task Should_create_snapshot_with_custom_name()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.SnapShots = [];

                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<TestAggregate>(options);

                var sut = new TestEventStream(document, dependencies);
                sut.SetAggregateType(typeInfo);

                var typedFactory = Substitute.For<IAggregateFactory<TestAggregate>>();
                dependencies.AggregateFactory.GetFactory<TestAggregate>().Returns(typedFactory);
                dependencies.DataStore.ReadAsync(document, 0, 5, Arg.Any<int?>()).Returns([]);
                var snapshotName = "CustomSnapshot";

                // Act
                await sut.Snapshot<TestAggregate>(5, snapshotName);

                // Assert
                await dependencies.SnapshotStore.Received(1).SetAsync(
                    Arg.Any<IBase>(), typeInfo, document, 5, snapshotName);
                Assert.Single(active.SnapShots);
                Assert.Equal(5, active.SnapShots[0].UntilVersion);
                Assert.Equal(snapshotName, active.SnapShots[0].Name);
            }

            [Fact]
            public async Task Should_handle_chunked_events()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.SnapShots = [];
                active.ChunkSettings = new StreamChunkSettings { EnableChunks = true };

                var chunk1 = new StreamChunk { ChunkIdentifier = 1 };
                var chunk2 = new StreamChunk { ChunkIdentifier = 2 };
                active.StreamChunks = [chunk1, chunk2];

                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<TestAggregate>(options);

                var sut = new TestEventStream(document, dependencies);
                sut.SetAggregateType(typeInfo);

                var mockAggregate = Substitute.For<TestAggregate>();
                var typedFactory = Substitute.For<IAggregateFactory<TestAggregate>>();
                typedFactory.Create(Arg.Any<IEventStream>()).Returns(mockAggregate);
                dependencies.AggregateFactory.GetFactory<TestAggregate>().Returns(typedFactory);

                var event1 = Substitute.For<IEvent>();
                var event2 = Substitute.For<IEvent>();
                var event3 = Substitute.For<IEvent>();
                dependencies.DataStore.ReadAsync(document, 0, 5, chunk: 1).Returns(new List<IEvent> { event1, event2 });
                dependencies.DataStore.ReadAsync(document, 0, 5, chunk: 2).Returns(new List<IEvent> { event3 });

                // Act
                await sut.Snapshot<TestAggregate>(5);

                // Assert
                await dependencies.SnapshotStore.Received(1).SetAsync(
                    Arg.Any<IBase>(), typeInfo, document, 5, null);
                await dependencies.ObjectDocumentFactory.Received(1).SetAsync(document);
            }

            [Fact]
            public async Task Should_handle_factory_returning_null()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var active = Substitute.For<StreamInformation>();
                document.Active.Returns(active);
                active.SnapShots = new List<StreamSnapShot>();

                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<TestAggregate>(options);

                var sut = new TestEventStream(document, dependencies);
                sut.SetAggregateType(typeInfo);

                dependencies.AggregateFactory.GetFactory<TestAggregate>()
                    .Returns((IAggregateFactory<TestAggregate>)null!);

                // Act & Assert
                await Assert.ThrowsAsync<NullReferenceException>(() => sut.Snapshot<TestAggregate>(5));
            }


        }

        public class GetSnapshot
        {
            [Fact]
            public async Task Should_throw_exception_when_get_snapshot_with_no_type_set()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                await Assert.ThrowsAsync<SnapshotJsonTypeInfoNotSetException>(() =>
                    sut.GetSnapShot(1));
            }

            [Fact]
            public async Task Should_call_snapshot_store_get_async_with_correct_parameters()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<object>(options);
                var sut = new TestEventStream(document, dependencies);
                sut.SetSnapShotType(typeInfo);

                // Set up return value for snapshot store
                var expectedSnapshot = new object();
                dependencies.SnapshotStore.GetAsync(typeInfo, document, 1, null)
                    .Returns(Task.FromResult<object?>(expectedSnapshot));

                // Act
                var result = await sut.GetSnapShot(1);

                // Assert
                Assert.Equal(expectedSnapshot, result);
                await dependencies.SnapshotStore.Received(1)
                    .GetAsync(typeInfo, document, 1, null);
            }

            [Fact]
            public async Task Should_call_snapshot_store_get_async_with_custom_name()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<object>(options);
                var sut = new TestEventStream(document, dependencies);
                sut.SetSnapShotType(typeInfo);
                var snapshotName = "CustomSnapshot";

                // Set up return value for snapshot store
                var expectedSnapshot = new object();
                dependencies.SnapshotStore.GetAsync(typeInfo, document, 1, snapshotName)
                    .Returns(Task.FromResult<object?>(expectedSnapshot));

                // Act
                var result = await sut.GetSnapShot(1, snapshotName);

                // Assert
                Assert.Equal(expectedSnapshot, result);
                await dependencies.SnapshotStore.Received(1)
                    .GetAsync(typeInfo, document, 1, snapshotName);
            }

            [Fact]
            public async Task Should_return_null_when_snapshot_store_returns_null()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<object>(options);
                var sut = new TestEventStream(document, dependencies);
                sut.SetSnapShotType(typeInfo);

                // Set up null return value for snapshot store
                dependencies.SnapshotStore.GetAsync(typeInfo, document, 1, null)
                    .Returns(Task.FromResult<object?>(null));

                // Act
                var result = await sut.GetSnapShot(1);

                // Assert
                Assert.Null(result);
                await dependencies.SnapshotStore.Received(1)
                    .GetAsync(typeInfo, document, 1, null);
            }
        }

        public class SetSnapShotType
        {

            [Fact]
            public void Should_throw_exception_when_set_snapshot_type_with_null()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.SetSnapShotType(null!));
            }

            [Fact]
            public void Should_set_json_type_info_snapshot_when_valid_type_info_provided()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfo = JsonTypeInfo.CreateJsonTypeInfo<object>(options);
                var sut = new TestEventStream(document, dependencies);

                // Act
                sut.SetSnapShotType(typeInfo);

                // Assert
                Assert.Equal(typeInfo, sut.GetJsonTypeInfoSnapshot());
            }
        }

        public class SetAggregateType
        {

            [Fact]
            public void Should_throw_exception_when_set_aggregate_type_with_null()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var sut = new TestEventStream(document, dependencies);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.SetAggregateType(null!));
            }

            [Fact]
            public void Should_set_aggregate_type()
            {
                // Arrange
                var document = Substitute.For<IObjectDocumentWithMethods>();
                var dependencies = Substitute.For<IStreamDependencies>();
                var options = new JsonSerializerOptions();
                var typeInfoInfo = JsonTypeInfo.CreateJsonTypeInfo<object>(options);
                var sut = new TestEventStream(document, dependencies);

                // Act
                sut.SetAggregateType(typeInfoInfo);

                // Assert
                Assert.Equal(typeInfoInfo, sut.GetJsonTypeInfoAgg());
            }
        }

        public class TestEventStream(IObjectDocumentWithMethods document, IStreamDependencies streamDependencies)
            : BaseEventStream(document, streamDependencies)
        {
            public Func<List<IAction>, ILeasedSession>? GetSessionCallback;

            public ILeasedSession GetSessionPublic(List<IAction> actions)
            {
                return GetSession(actions);
            }

            protected new ILeasedSession GetSession(List<IAction> actions)
            {
                if (GetSessionCallback != null)
                {
                    return GetSessionCallback(actions);
                }
                return base.GetSession(actions);
            }

            public List<IAction> GetActions() => Actions;
            public List<INotification> GetNotifications() => Notifications;
            public List<IEventUpcaster> GetUpcasters() => UpCasters;
            public JsonTypeInfo? GetJsonTypeInfoSnapshot() => JsonTypeInfoSnapshot;
            public JsonTypeInfo? GetJsonTypeInfoAgg() => JsonTypeInfoAgg;
        }

        public class TestAggregate : IBase
        {
            public Task Fold()
            {
                return Task.CompletedTask;
            }

            public void Fold(IEvent @event) { }
            public void ProcessSnapshot(object snapshot)
            {
            }
        }



        private record TestEvent : Event<object> { }
    }
}
