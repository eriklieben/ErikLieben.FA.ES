using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Action
{
    public class StreamActionDataStoreTests
    {
        public class AppendAsyncMethod
        {
            [Fact]
            public async Task Should_delegate_to_underlying_datastore()
            {
                // Arrange
                var mockDataStore = Substitute.For<IDataStore>();
                var document = Substitute.For<IObjectDocument>();
                var events = new IEvent[] { Substitute.For<IEvent>() };
                var sut = new StreamActionDataStore(mockDataStore);

                // Act
                await sut.AppendAsync(document, default, events);

                // Assert
                await mockDataStore.Received(1).AppendAsync(document, default, events);
            }

            [Fact]
            public async Task Should_pass_multiple_events_to_underlying_datastore()
            {
                // Arrange
                var mockDataStore = Substitute.For<IDataStore>();
                var document = Substitute.For<IObjectDocument>();
                var events = new IEvent[] {
                    Substitute.For<IEvent>(),
                    Substitute.For<IEvent>(),
                    Substitute.For<IEvent>()
                };
                var sut = new StreamActionDataStore(mockDataStore);

                // Act
                await sut.AppendAsync(document, default, events);

                // Assert
                await mockDataStore.Received(1).AppendAsync(document, default, events);
            }

            [Fact]
            public async Task Should_pass_empty_events_array_to_underlying_datastore()
            {
                // Arrange
                var mockDataStore = Substitute.For<IDataStore>();
                var document = Substitute.For<IObjectDocument>();
                var events = Array.Empty<IEvent>();
                var sut = new StreamActionDataStore(mockDataStore);

                // Act
                await sut.AppendAsync(document, default, events);

                // Assert
                await mockDataStore.Received(1).AppendAsync(document, default, events);
            }
        }

        public class ReadAsyncMethod
        {
            [Fact]
            public async Task Should_delegate_to_underlying_datastore_with_default_parameters()
            {
                // Arrange
                var mockDataStore = Substitute.For<IDataStore>();
                var document = Substitute.For<IObjectDocument>();
                var expectedEvents = new List<IEvent> { Substitute.For<IEvent>() };
                mockDataStore.ReadAsync(document, 0, null, null)
                    .Returns(Task.FromResult<IEnumerable<IEvent>?>(expectedEvents));
                var sut = new StreamActionDataStore(mockDataStore);

                // Act
                var result = await sut.ReadAsync(document);

                // Assert
                await mockDataStore.Received(1).ReadAsync(document, 0, null, null);
                Assert.Equal(expectedEvents, result);
            }

            [Fact]
            public async Task Should_delegate_to_underlying_datastore_with_custom_start_version()
            {
                // Arrange
                var mockDataStore = Substitute.For<IDataStore>();
                var document = Substitute.For<IObjectDocument>();
                int startVersion = 10;
                var expectedEvents = new List<IEvent> { Substitute.For<IEvent>() };
                mockDataStore.ReadAsync(document, startVersion, null, null)
                    .Returns(Task.FromResult<IEnumerable<IEvent>?>(expectedEvents));
                var sut = new StreamActionDataStore(mockDataStore);

                // Act
                var result = await sut.ReadAsync(document, startVersion);

                // Assert
                await mockDataStore.Received(1).ReadAsync(document, startVersion, null, null);
                Assert.Equal(expectedEvents, result);
            }

            [Fact]
            public async Task Should_delegate_to_underlying_datastore_with_all_parameters()
            {
                // Arrange
                var mockDataStore = Substitute.For<IDataStore>();
                var document = Substitute.For<IObjectDocument>();
                int startVersion = 10;
                int? untilVersion = 20;
                int? chunk = 5;
                var expectedEvents = new List<IEvent> { Substitute.For<IEvent>() };
                mockDataStore.ReadAsync(document, startVersion, untilVersion, chunk)
                    .Returns(Task.FromResult<IEnumerable<IEvent>?>(expectedEvents));
                var sut = new StreamActionDataStore(mockDataStore);

                // Act
                var result = await sut.ReadAsync(document, startVersion, untilVersion, chunk);

                // Assert
                await mockDataStore.Received(1).ReadAsync(document, startVersion, untilVersion, chunk);
                Assert.Equal(expectedEvents, result);
            }

            [Fact]
            public async Task Should_return_null_when_underlying_datastore_returns_null()
            {
                // Arrange
                var mockDataStore = Substitute.For<IDataStore>();
                var document = Substitute.For<IObjectDocument>();
                mockDataStore.ReadAsync(document, 0, null, null)
                    .Returns(Task.FromResult<IEnumerable<IEvent>?>(null));
                var sut = new StreamActionDataStore(mockDataStore);

                // Act
                var result = await sut.ReadAsync(document);

                // Assert
                await mockDataStore.Received(1).ReadAsync(document, 0, null, null);
                Assert.Null(result);
            }
        }
    }
}
