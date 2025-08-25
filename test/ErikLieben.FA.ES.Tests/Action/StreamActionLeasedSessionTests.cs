using ErikLieben.FA.ES.Actions;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Action
{
    public class StreamActionLeasedSessionTests
    {
        public class Append
        {
            [Fact]
            public void Should_delegate_to_underlying_session()
            {
                // Arrange
                var mockSession = Substitute.For<ILeasedSession>();
                var sut = new StreamActionLeasedSession(mockSession);
                var payload = new TestPayload();
                var actionMetadata = new ActionMetadata();
                var overrideEventType = "TestEventType";
                var externalSequencer = "ExternalSequencer";
                var metadata = new Dictionary<string, string> { { "key", "value" } };
                var expectedEvent = Substitute.For<IEvent<TestPayload>>();

                mockSession.Append(payload, actionMetadata, overrideEventType, externalSequencer, metadata)
                    .Returns(expectedEvent);

                // Act
                var result = sut.Append(payload, actionMetadata, overrideEventType, externalSequencer, metadata);

                // Assert
                Assert.Same(expectedEvent, result);
                mockSession.Received(1).Append(payload, actionMetadata, overrideEventType, externalSequencer, metadata);
            }

            [Fact]
            public void Should_handle_null_optional_parameters()
            {
                // Arrange
                var mockSession = Substitute.For<ILeasedSession>();
                var sut = new StreamActionLeasedSession(mockSession);
                var payload = new TestPayload();
                var expectedEvent = Substitute.For<IEvent<TestPayload>>();

                mockSession.Append(payload, null, null, null, null)
                    .Returns(expectedEvent);

                // Act
                var result = sut.Append(payload);

                // Assert
                Assert.Same(expectedEvent, result);
                mockSession.Received(1).Append(payload, null, null, null, null);
            }
        }

        public class Buffer
        {
            [Fact]
            public void Should_return_underlying_session_buffer()
            {
                // Arrange
                var mockSession = Substitute.For<ILeasedSession>();
                var expectedBuffer = new List<JsonEvent>();
                mockSession.Buffer.Returns(expectedBuffer);
                var sut = new StreamActionLeasedSession(mockSession);

                // Act
                var result = sut.Buffer;

                // Assert
                Assert.Same(expectedBuffer, result);
            }
        }

        public class CommitAsync
        {
            [Fact]
            public async Task Should_delegate_to_underlying_session()
            {
                // Arrange
                var mockSession = Substitute.For<ILeasedSession>();
                var sut = new StreamActionLeasedSession(mockSession);
                var expectedTask = Task.CompletedTask;
                mockSession.CommitAsync().Returns(expectedTask);

                // Act
                await sut.CommitAsync();

                // Assert
                await mockSession.Received(1).CommitAsync();
            }
        }

        public class IsTerminatedASync
        {
            [Fact]
            public async Task Should_delegate_to_underlying_session()
            {
                // Arrange
                var mockSession = Substitute.For<ILeasedSession>();
                var streamIdentifier = "streamId";
                mockSession.IsTerminatedASync(streamIdentifier).Returns(Task.FromResult(true));
                var sut = new StreamActionLeasedSession(mockSession);

                // Act
                var result = await sut.IsTerminatedASync(streamIdentifier);

                // Assert
                Assert.True(result);
                await mockSession.Received(1).IsTerminatedASync(streamIdentifier);
            }
        }

        public class ReadAsync
        {
            [Fact]
            public async Task Should_delegate_to_underlying_session_with_default_parameters()
            {
                // Arrange
                var mockSession = Substitute.For<ILeasedSession>();
                var expectedEvents = Substitute.For<IEnumerable<IEvent>>();
                mockSession.ReadAsync(0, null).Returns(Task.FromResult(expectedEvents));
                var sut = new StreamActionLeasedSession(mockSession);

                // Act
                var result = await sut.ReadAsync();

                // Assert
                Assert.Same(expectedEvents, result);
                await mockSession.Received(1).ReadAsync(0, null);
            }

            [Fact]
            public async Task Should_delegate_to_underlying_session_with_specified_parameters()
            {
                // Arrange
                var mockSession = Substitute.For<ILeasedSession>();
                var expectedEvents = Substitute.For<IEnumerable<IEvent>>();
                mockSession.ReadAsync(5, 10).Returns(Task.FromResult(expectedEvents));
                var sut = new StreamActionLeasedSession(mockSession);

                // Act
                var result = await sut.ReadAsync(5, 10);

                // Assert
                Assert.Same(expectedEvents, result);
                await mockSession.Received(1).ReadAsync(5, 10);
            }
        }

        public class TestPayload
        {
            public string Value { get; set; } = "Test";
        }
    }
}
