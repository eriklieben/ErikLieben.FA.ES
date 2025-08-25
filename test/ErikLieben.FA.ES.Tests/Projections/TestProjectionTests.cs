using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Projections
{
    public class ProjectionTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_initialize_with_parameterless_constructor()
            {
                // Arrange & Act
                var sut = new TestProjection();

                // Assert
                Assert.NotNull(sut);
                Assert.Null(sut.DocumentFactoryProperty);
                Assert.Null(sut.EventStreamFactoryProperty);
            }

            [Fact]
            public void Should_initialize_with_two_parameters_constructor()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                // Act
                var sut = new TestProjection(documentFactory, eventStreamFactory);

                // Assert
                Assert.NotNull(sut);
                Assert.Same(documentFactory, sut.DocumentFactoryProperty);
                Assert.Same(eventStreamFactory, sut.EventStreamFactoryProperty);
            }

            [Fact]
            public void Should_initialize_with_full_parameters_constructor()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var checkpoint = new Checkpoint();
                var checkpointFingerprint = "test-fingerprint";

                // Act
                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, checkpointFingerprint);

                // Assert
                Assert.NotNull(sut);
                Assert.Same(documentFactory, sut.DocumentFactoryProperty);
                Assert.Same(eventStreamFactory, sut.EventStreamFactoryProperty);
                Assert.Same(checkpoint, sut.Checkpoint);
                Assert.Equal(checkpointFingerprint, sut.CheckpointFingerprint);
            }
        }

        public class FoldMethods
        {
            [Fact]
            public async Task Should_call_generic_fold_when_non_generic_overload_called()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var sut = new TestProjection(documentFactory, eventStreamFactory);
                var @event = Substitute.For<IEvent>();
                var document = Substitute.For<IObjectDocument>();

                // Act
                await sut.Fold(@event, document);

                // Assert
                Assert.Equal(1, sut.FoldCallCount);
                Assert.Same(@event, sut.LastFoldEvent);
                Assert.Same(document, sut.LastFoldDocument);
                Assert.Null(sut.LastFoldData);
                Assert.Null(sut.LastFoldContext);
            }

            [Fact]
            public async Task Should_call_generic_fold_when_context_overload_called()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var sut = new TestProjection(documentFactory, eventStreamFactory);
                var @event = Substitute.For<IEvent>();
                var document = Substitute.For<IObjectDocument>();
                var context = Substitute.For<IExecutionContext>();

                // Act
                await sut.FoldWithContext(@event, document, context);

                // Assert
                Assert.Equal(1, sut.FoldCallCount);
                Assert.Same(@event, sut.LastFoldEvent);
                Assert.Same(document, sut.LastFoldDocument);
                Assert.Null(sut.LastFoldData);
                Assert.Same(context, sut.LastFoldContext);
            }

            [Fact]
            public async Task Should_process_generic_fold_with_all_parameters()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var sut = new TestProjection(documentFactory, eventStreamFactory);
                var @event = Substitute.For<IEvent>();
                var document = Substitute.For<IObjectDocument>();
                var data = new TestData { Name = "Test" };
                var context = Substitute.For<IExecutionContext>();

                // Act
                await sut.Fold(@event, document, data, context);

                // Assert
                Assert.Equal(1, sut.FoldCallCount);
                Assert.Same(@event, sut.LastFoldEvent);
                Assert.Same(document, sut.LastFoldDocument);
                Assert.Same(data, sut.LastFoldData);
                Assert.Same(context, sut.LastFoldContext);
            }
        }

        public class IsNewerMethod
        {
            [Fact]
            public void Should_return_true_when_token_identifier_not_in_checkpoint()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var existingIdentifier = new ObjectIdentifier("Account", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { existingIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var newObjectId = Guid.NewGuid().ToString();
                var newIdentifier = new ObjectIdentifier("NewObject", newObjectId);
                var token = new VersionToken(newIdentifier, versionIdentifier);

                // Act
                var result = sut.IsNewerPublic(token);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void Should_return_true_when_token_is_newer()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId, versionId, 2);

                // Act
                var result = sut.IsNewerPublic(token);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void Should_return_false_when_token_is_not_newer()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 2);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId, versionId, 1);

                // Act
                var result = sut.IsNewerPublic(token);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void Should_return_false_when_token_is_same_version()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId, versionId, 1);

                // Act
                var result = sut.IsNewerPublic(token);

                // Assert
                Assert.False(result);
            }
        }

        public class GetWhenParameterValueMethod
        {
            [Fact]
            public void Should_return_null_when_factory_not_found()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var sut = new TestProjection(documentFactory, eventStreamFactory);
                var document = Substitute.For<IObjectDocument>();
                var @event = Substitute.For<IEvent>();

                // Act
                var result = sut.GetWhenParameterValuePublic<TestData, TestEventData>("unknown-type", document, @event);

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void Should_use_factory_with_event_when_available()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var sut = new TestProjection(documentFactory, eventStreamFactory);
                var document = Substitute.For<IObjectDocument>();
                var eventData = new TestEventData { Value = "test-value" };
                var @event = Substitute.For<IEvent<TestEventData>>();
                @event.Data().Returns(eventData);
                var expectedResult = new TestData { Name = "test-result" };

                var factory = Substitute.For<IProjectionWhenParameterValueFactory<TestData, TestEventData>>();
                factory.Create(document, @event).Returns(expectedResult);

                sut.AddWhenParameterValueFactory("test-type", factory);

                // Act
                var result = sut.GetWhenParameterValuePublic<TestData, TestEventData>("test-type", document, @event);

                // Assert
                Assert.Same(expectedResult, result);
                factory.Received(1).Create(document, @event);
            }

            [Fact]
            public void Should_use_factory_without_event_when_available()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var sut = new TestProjection(documentFactory, eventStreamFactory);
                var document = Substitute.For<IObjectDocument>();
                var @event = Substitute.For<IEvent>();
                var expectedResult = new TestData { Name = "test-result" };

                var factory = Substitute.For<IProjectionWhenParameterValueFactory<TestData>>();
                factory.Create(document, @event).Returns(expectedResult);

                sut.AddWhenParameterValueFactory("test-type", factory);

                // Act
                var result = sut.GetWhenParameterValuePublic<TestData, TestEventData>("test-type", document, @event);

                // Assert
                Assert.Same(expectedResult, result);
                factory.Received(1).Create(document, @event);
            }

            [Fact]
            public void Should_return_null_when_factory_type_not_expected()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();
                var sut = new TestProjection(documentFactory, eventStreamFactory);
                var document = Substitute.For<IObjectDocument>();
                var @event = Substitute.For<IEvent>();

                // Create a factory of a different type than expected
                var unknownFactory = Substitute.For<IProjectionWhenParameterValueFactory>();

                sut.AddWhenParameterValueFactory("test-type", unknownFactory);

                // Act
                var result = sut.GetWhenParameterValuePublic<TestData, TestEventData>("test-type", document, @event);

                // Assert
                Assert.Null(result);
            }
        }

        public class UpdateToVersionMethod
        {
            [Fact]
            public async Task Should_throw_exception_when_factories_are_null()
            {
                // Arrange
                var sut = new TestProjection();
                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 2);
                var token = new VersionToken(objectIdentifier, versionIdentifier);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<Exception>(() => sut.UpdateToVersion(token));
                Assert.Equal("documentFactory or eventStreamFactory is null", exception.Message);
            }

            [Fact]
            public async Task Should_not_process_when_token_is_not_newer()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 2);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId, versionId, 1);
                token.ToLatestVersion();

                // Act
                await sut.UpdateToVersion(token);

                // Assert
                await documentFactory.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<string>());
                Assert.Equal(0, sut.FoldCallCount);
            }

            [Fact]
            public async Task Should_process_when_token_is_newer()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId, versionId, 2);
                var document = Substitute.For<IObjectDocument>();
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns(objectId);
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.Active.StreamIdentifier = Guid.NewGuid().ToString();
                documentFactory.GetAsync(token.ObjectName, token.ObjectId).Returns(document);

                var eventStream = Substitute.For<IEventStream>();
                eventStreamFactory.Create(document).Returns(eventStream);

                var events = new List<IEvent> { Substitute.For<IEvent>() };
                eventStream.ReadAsync(2, 2).Returns(events);

                // Act
                await sut.UpdateToVersion(token);

                // Assert
                await documentFactory.Received(1).GetAsync(token.ObjectName, token.ObjectId);
                await eventStream.Received(1).ReadAsync(2, 2);
                Assert.Equal(1, sut.FoldCallCount);
                Assert.Equal(1, sut.PostWhenAllCallCount);
            }

            [Fact]
            public async Task Should_process_when_try_update_to_latest_version_is_true()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId, versionId, 1);

                var document = Substitute.For<IObjectDocument>();
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns(objectId);
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.Active.StreamIdentifier = Guid.NewGuid().ToString();
                documentFactory.GetAsync(token.ObjectName, token.ObjectId).Returns(document);

                var eventStream = Substitute.For<IEventStream>();
                eventStreamFactory.Create(document).Returns(eventStream);

                var events = new List<IEvent> { Substitute.For<IEvent>() };
                eventStream.ReadAsync(2).Returns(events);

                // Act
                await sut.UpdateToVersion(token.ToLatestVersion());

                // Assert
                await documentFactory.Received(1).GetAsync(token.ObjectName, token.ObjectId);
                await eventStream.Received(1).ReadAsync(2);
                Assert.Equal(1, sut.FoldCallCount);
                Assert.Equal(1, sut.PostWhenAllCallCount);
            }

            [Fact]
            public async Task Should_update_checkpoint_and_fingerprint_after_processing()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId, versionId,  2);

                var document = Substitute.For<IObjectDocument>();
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns(objectId);
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.Active.StreamIdentifier = Guid.NewGuid().ToString();
                documentFactory.GetAsync(token.ObjectName, token.ObjectId).Returns(document);

                var eventStream = Substitute.For<IEventStream>();
                eventStreamFactory.Create(document).Returns(eventStream);

                var @event = Substitute.For<IEvent>();
                @event.EventVersion.Returns(2);
                var events = new List<IEvent> { @event };
                eventStream.ReadAsync(2, 2).Returns(events);

                // Act
                await sut.UpdateToVersion(token);

                // Assert
                var identifier = new ObjectIdentifier("TestObject", objectId);
                Assert.Contains(identifier, sut.Checkpoint.Keys);
                Assert.NotNull(sut.Checkpoint[identifier]);
                Assert.Equal("00000000000000000002", sut.Checkpoint[identifier].VersionString);
                Assert.NotNull(sut.CheckpointFingerprint);
            }
        }

        public class UpdateToVersionWithDataMethod
        {
            [Fact]
            public async Task Should_throw_exception_when_factories_are_null()
            {
                // Arrange
                var sut = new TestProjection();

                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var token = new VersionToken("TestObject", Guid.NewGuid().ToString(), versionId, 1);
                var data = new TestData();

                // Act & Assert
                var exception = await Assert.ThrowsAsync<Exception>(() =>
                    sut.UpdateToVersion<TestData>(token, null, data));
                Assert.Equal("documentFactory or eventStreamFactory is null", exception.Message);
            }

            [Fact]
            public async Task Should_throw_exception_when_event_loops()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId,versionId, 2);
                var document = Substitute.For<IObjectDocument>();
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns(objectId);
                documentFactory.GetAsync(token.ObjectName, token.ObjectId).Returns(document);

                var eventStream = Substitute.For<IEventStream>();
                eventStreamFactory.Create(document).Returns(eventStream);

                var @event = Substitute.For<IEvent>();
                var events = new List<IEvent> { @event };
                eventStream.ReadAsync(2, 2).Returns(events);

                var context = Substitute.For<IExecutionContextWithData<TestData>>();
                context.Event.Returns(@event); // Same event in context and in stream

                // Act & Assert
                var exception = await Assert.ThrowsAsync<Exception>(() =>
                    sut.UpdateToVersion<TestData>(token, context, null));
                Assert.Equal("parent event is same as current event, are you running into a loop?", exception.Message);
            }

            [Fact]
            public async Task Should_process_with_data_and_context()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var token = new VersionToken("TestObject", objectId, versionId, 2);

                var document = Substitute.For<IObjectDocument>();
                document.ObjectName.Returns("TestObject");
                document.ObjectId.Returns(objectId);
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.Active.StreamIdentifier = Guid.NewGuid().ToString();
                documentFactory.GetAsync(token.ObjectName, token.ObjectId).Returns(document);

                var eventStream = Substitute.For<IEventStream>();
                eventStreamFactory.Create(document).Returns(eventStream);

                var @event = Substitute.For<IEvent>();
                var events = new List<IEvent> { @event };
                eventStream.ReadAsync(2, 2).Returns(events);

                var data = new TestData { Name = "test-data" };
                var context = Substitute.For<IExecutionContextWithData<TestData>>();
                context.Event.Returns(Substitute.For<IEvent>()); // Different event

                // Act
                await sut.UpdateToVersion<TestData>(token, context, data);

                // Assert
                Assert.Equal(1, sut.FoldCallCount);
                Assert.Same(data, sut.LastFoldData);
                Assert.Same(context, sut.LastFoldContext);
                Assert.Equal(1, sut.PostWhenAllCallCount);
            }
        }

        public class UpdateToLatestVersionMethod
        {
            [Fact]
            public async Task Should_update_all_tokens_in_checkpoint()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId1 = Guid.NewGuid().ToString();
                var objectIdentifier1 = new ObjectIdentifier("ObjectName1", objectId1);
                var versionId1 = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier1 = new VersionIdentifier(versionId1, 1);

                var objectId2 = Guid.NewGuid().ToString();
                var objectIdentifier2 = new ObjectIdentifier("ObjectName2", objectId2);
                var versionId2 = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier2 = new VersionIdentifier(versionId2, 2);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier1, versionIdentifier1 },
                    { objectIdentifier2, versionIdentifier2 }
                };

                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
                var context = Substitute.For<IExecutionContext>();

                // Setup for first token
                var document1 = Substitute.For<IObjectDocument>();
                document1.ObjectName.Returns("ObjectName1");
                document1.ObjectId.Returns(objectId1);
                documentFactory.GetAsync("ObjectName1", objectId1).Returns(document1);
                var eventStream1 = Substitute.For<IEventStream>();
                eventStreamFactory.Create(document1).Returns(eventStream1);
                eventStream1.ReadAsync(2).Returns(new List<IEvent>());

                // Setup for the second token
                var document2 = Substitute.For<IObjectDocument>();
                document2.ObjectName.Returns("ObjectName2");
                document2.ObjectId.Returns(objectId2);
                documentFactory.GetAsync("ObjectName2", objectId2).Returns(document2);
                var eventStream2 = Substitute.For<IEventStream>();
                eventStreamFactory.Create(document2).Returns(eventStream2);
                eventStream2.ReadAsync(3).Returns(new List<IEvent>());

                // Act
                await sut.UpdateToLatestVersion(context);

                // Assert
                Assert.Equal(0, sut.FoldCallCount); // No events were returned
                Assert.Equal(0, sut.PostWhenAllCallCount); // No events were processed
            }
        }

        public class ToJsonMethod
        {
            [Fact]
            public void Should_serialize_projection_to_json()
            {
                // Arrange
                var documentFactory = Substitute.For<IObjectDocumentFactory>();
                var eventStreamFactory = Substitute.For<IEventStreamFactory>();

                var objectId = Guid.NewGuid().ToString();
                var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
                var versionId = Guid.NewGuid().ToString().Replace("-", "");
                var versionIdentifier = new VersionIdentifier(versionId, 1);

                var checkpoint = new Checkpoint
                {
                    { objectIdentifier, versionIdentifier }
                };

                var checkpointFingerprint = "test-fingerprint";
                var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, checkpointFingerprint);
                var expectedJson =
                    "{\"Test\":\"Value\",\"$checkpoint\":{\"TestObject_test-id\":\"test-version\"},\"$checkpointFingerprint\":\"test-fingerprint\"}";
                sut.JsonResult = expectedJson;

                // Act
                var result = sut.ToJson();

                // Assert
                Assert.Equal(expectedJson, result);
            }
        }


        private class TestProjection : Projection
        {
            public int FoldCallCount { get; private set; }
            public IEvent? LastFoldEvent { get; private set; }
            public IObjectDocument? LastFoldDocument { get; private set; }
            public object? LastFoldData { get; private set; }
            public IExecutionContext? LastFoldContext { get; private set; }
            public int PostWhenAllCallCount { get; private set; }
            public string JsonResult { get; set; } = "{}";

            public IObjectDocumentFactory? DocumentFactoryProperty => DocumentFactory;
            public IEventStreamFactory? EventStreamFactoryProperty => EventStreamFactory;

            private readonly Dictionary<string, IProjectionWhenParameterValueFactory> whenParameterValueFactories = new();

            protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories =>
                whenParameterValueFactories;

            private Checkpoint checkpoint = new();

            public override Checkpoint Checkpoint
            {
                get => checkpoint;
                set => checkpoint = value;
            }

            public TestProjection() : base()
            {
            }

            public TestProjection(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                : base(documentFactory, eventStreamFactory)
            {
            }

            public TestProjection(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory,
                Checkpoint checkpoint, string? checkpointFingerprint)
                : base(documentFactory, eventStreamFactory, checkpoint, checkpointFingerprint)
            {
            }

            public override async Task Fold<T>(IEvent @event, IObjectDocument document, T? data = null,
                IExecutionContext? context = null) where T : class
            {
                FoldCallCount++;
                LastFoldEvent = @event;
                LastFoldDocument = document;
                LastFoldData = data;
                LastFoldContext = context;
                await Task.CompletedTask;
            }

            public async Task FoldWithContext(IEvent @event, IObjectDocument document, IExecutionContext context)
            {
                await base.Fold(@event, document, context);
            }

            public override string ToJson()
            {
                return JsonResult;
            }

            protected override async Task PostWhenAll(IObjectDocument document)
            {
                PostWhenAllCallCount++;
                await Task.CompletedTask;
            }

            public bool IsNewerPublic(VersionToken token)
            {
                return IsNewer(token);
            }

            public T? GetWhenParameterValuePublic<T, Te>(string forType, IObjectDocument document, IEvent @event)
                where Te : class where T : class
            {
                return GetWhenParameterValue<T, Te>(forType, document, @event);
            }

            public void AddWhenParameterValueFactory(string key, IProjectionWhenParameterValueFactory factory)
            {
                whenParameterValueFactories[key] = factory;
            }
        }

        public class TestData
        {
            public string Name { get; set; } = string.Empty;
        }

        public class TestEventData
        {
            public string Value { get; set; } = string.Empty;
        }
    }
}
