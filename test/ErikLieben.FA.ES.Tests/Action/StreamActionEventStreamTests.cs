using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Processors;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Action
{
    public class StreamActionEventStreamTests
    {
        public class Constructor
        {
            // [Fact]
            // public void Should_create_instance_with_supplied_event_stream()
            // {
            //     // Arrange
            //     var eventStream = Substitute.For<IEventStream>();
            //     var document = Substitute.For<IObjectDocumentWithMethods>();
            //     var dependencies = new List<Guid>();
            //     eventStream.Document.Returns(document);
            //     eventStream.StreamDependencies.Returns(dependencies);
            //
            //     // Act
            //     var sut = new StreamActionEventStream(eventStream);
            //
            //     // Assert
            //     Assert.NotNull(sut);
            //     Assert.Same(document, sut.Document);
            // }
        }

        public class SettingsProperty
        {
            [Fact]
            public void Should_return_underlying_event_stream_settings()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var settings = Substitute.For<IEventStreamSettings>();
                eventStream.Settings.Returns(settings);
                var sut = new StreamActionEventStream(eventStream);

                // Act
                var result = sut.Settings;

                // Assert
                Assert.Same(settings, result);
            }
        }

        public class DocumentProperty
        {
            [Fact]
            public void Should_return_underlying_event_stream_document()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var document = Substitute.For<IObjectDocumentWithMethods>();
                eventStream.Document.Returns(document);
                var sut = new StreamActionEventStream(eventStream);

                // Act
                var result = sut.Document;

                // Assert
                Assert.Same(document, result);
            }
        }

        public class RegisterEventMethod
        {
            // [Fact]
            // public void Should_delegate_to_underlying_event_stream()
            // {
            //     // Arrange
            //     var eventStream = Substitute.For<IEventStream>();
            //     var sut = new StreamActionEventStream(eventStream);
            //     const string eventName = "TestEvent";
            //
            //     // Act
            //     sut.RegisterEvent<TestEvent>(eventName);
            //
            //     // Assert
            //     eventStream.Received(1).RegisterEvent<TestEvent>(eventName);
            // }

            private class TestEvent { }
        }

        public class RegisterActionMethod
        {
            [Fact]
            public void Should_delegate_to_underlying_event_stream()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var sut = new StreamActionEventStream(eventStream);
                var action = Substitute.For<IAction>();

                // Act
                sut.RegisterAction(action);

                // Assert
                eventStream.Received(1).RegisterAction(action);
            }
        }

        public class RegisterNotificationMethod
        {
            [Fact]
            public void Should_delegate_to_underlying_event_stream()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var sut = new StreamActionEventStream(eventStream);
                var notification = Substitute.For<INotification>();

                // Act
                sut.RegisterNotification(notification);

                // Assert
                eventStream.Received(1).RegisterNotification(notification);
            }
        }

        public class RegisterPostAppendActionMethod
        {
            [Fact]
            public void Should_delegate_to_underlying_event_stream()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var sut = new StreamActionEventStream(eventStream);
                var action = Substitute.For<IPostAppendAction>();

                // Act
                sut.RegisterPostAppendAction(action);

                // Assert
                eventStream.Received(1).RegisterPostAppendAction(action);
            }
        }

        public class RegisterPreAppendActionMethod
        {
            [Fact]
            public void Should_delegate_to_underlying_event_stream()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var sut = new StreamActionEventStream(eventStream);
                var action = Substitute.For<IPreAppendAction>();

                // Act
                sut.RegisterPreAppendAction(action);

                // Assert
                eventStream.Received(1).RegisterPreAppendAction(action);
            }
        }

        public class RegisterPostReadActionMethod
        {
            [Fact]
            public void Should_delegate_to_underlying_event_stream()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var sut = new StreamActionEventStream(eventStream);
                var action = Substitute.For<IPostReadAction>();

                // Act
                sut.RegisterPostReadAction(action);

                // Assert
                eventStream.Received(1).RegisterPostReadAction(action);
            }
        }

        public class ReadAsyncMethod
        {
            [Fact]
            public async Task Should_delegate_to_underlying_event_stream()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var events = new List<IEvent>().AsReadOnly();
                eventStream.ReadAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<bool>()).Returns(events);
                var sut = new StreamActionEventStream(eventStream);
                const int startVersion = 5;
                const int untilVersion = 10;
                const bool useExternalSequencer = true;

                // Act
                var result = await sut.ReadAsync(startVersion, untilVersion, useExternalSequencer);

                // Assert
                Assert.Same(events, result);
                await eventStream.Received(1).ReadAsync(startVersion, untilVersion, useExternalSequencer);
            }
        }

        public class SessionMethod
        {
            // [Fact]
            // public async Task Should_create_stream_action_leased_session_and_commit()
            // {
            //     // Arrange
            //     var eventStream = Substitute.For<IEventStream>();
            //     var session = Substitute.For<ILeasedSession>();
            //     var sut = new StreamActionEventStream(eventStream);
            //
            //     // Mocking the GetSession method since it's likely protected
            //     // This is a bit tricky because we can't directly mock it
            //     // In a real scenario, we might need to create a test-specific wrapper
            //
            //     bool contextCalled = false;
            //     Action<ILeasedSession> context = leasedSession =>
            //     {
            //         contextCalled = true;
            //         Assert.NotNull(leasedSession);
            //         Assert.IsType<StreamActionLeasedSession>(leasedSession);
            //     };
            //
            //     // Act
            //     await sut.Session(context);
            //
            //     // Assert
            //     Assert.True(contextCalled, "Context should have been called");
            //     // We can't verify GetSession was called since it's likely protected
            //     // In a real scenario with more code access, additional assertions would be made
            // }
        }

        public class SnapshotMethod
        {
            [Fact]
            public async Task Should_delegate_to_underlying_event_stream()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var sut = new StreamActionEventStream(eventStream);
                const int untilVersion = 10;
                const string name = "TestSnapshot";

                // Act
                await sut.Snapshot<TestAggregate>(untilVersion, name);

                // Assert
                await eventStream.Received(1).Snapshot<TestAggregate>(untilVersion, name);
            }

            private class TestAggregate : IBase
            {
                public Guid Id { get; }
                public Task Fold()
                {
                    return Task.CompletedTask;
                }

                public void Fold(IEvent @event)
                {
                }

                public void ProcessSnapshot(object snapshot)
                {
                }
            }
        }

        public class GetSnapShotMethod
        {
            [Fact]
            public async Task Should_delegate_to_underlying_event_stream()
            {
                // Arrange
                var eventStream = Substitute.For<IEventStream>();
                var snapshotObject = new object();
                eventStream.GetSnapShot(Arg.Any<int>(), Arg.Any<string>()).Returns(snapshotObject);
                var sut = new StreamActionEventStream(eventStream);
                const int version = 10;
                const string name = "TestSnapshot";

                // Act
                var result = await sut.GetSnapShot(version, name);

                // Assert
                Assert.Same(snapshotObject, result);
                await eventStream.Received(1).GetSnapShot(version, name);
            }
        }

        public class SetSnapShotTypeMethod
        {
            // [Fact]
            // public void Should_delegate_to_underlying_event_stream()
            // {
            //     // Arrange
            //     var eventStream = Substitute.For<IEventStream>();
            //     var sut = new StreamActionEventStream(eventStream);
            //     var typeInfo = Substitute.For<JsonTypeInfo>();
            //     const string version = "v1";
            //
            //     // Act
            //     sut.SetSnapShotType(typeInfo, version);
            //
            //     // Assert
            //     eventStream.Received(1).SetSnapShotType(typeInfo, version);
            // }
        }

        public class SetAggregateTypeMethod
        {
            // [Fact]
            // public void Should_delegate_to_underlying_event_stream()
            // {
            //     // Arrange
            //     var eventStream = Substitute.For<IEventStream>();
            //     var sut = new StreamActionEventStream(eventStream);
            //     var typeInfo = Substitute.For<JsonTypeInfo>();
            //
            //     // Act
            //     sut.SetAggregateType(typeInfo);
            //
            //     // Assert
            //     eventStream.Received(1).SetAggregateType(typeInfo);
            // }
        }
    }
}
