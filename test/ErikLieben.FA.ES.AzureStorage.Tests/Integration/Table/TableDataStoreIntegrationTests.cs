using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Extensions.Azure;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Integration.Table;

/// <summary>
/// Integration tests for TableDataStore using Azurite TestContainer.
/// Tests event stream read/append operations against real table storage.
/// </summary>
[Collection("AzuriteIntegration")]
[Trait("Category", "Integration")]
[Trait("Feature", "TableStorage")]
public class TableDataStoreIntegrationTests : IAsyncLifetime
{
    private readonly AzuriteContainerFixture _fixture;
    private readonly string _testId;
    private TableDataStore? _dataStore;
    private EventStreamTableSettings? _tableSettings;

    public TableDataStoreIntegrationTests(AzuriteContainerFixture fixture)
    {
        _fixture = fixture;
        _testId = Guid.NewGuid().ToString("N")[..8];
    }

    public async Task InitializeAsync()
    {
        var tableClientFactory = CreateTableClientFactory(_fixture.TableServiceClient!);

        _tableSettings = new EventStreamTableSettings(
            defaultDataStore: "default",
            autoCreateTable: true,
            defaultEventTableName: $"eventstream{_testId}");

        _dataStore = new TableDataStore(tableClientFactory, _tableSettings);

        // Pre-create the event table
        var tableClient = _fixture.TableServiceClient!.GetTableClient(_tableSettings.DefaultEventTableName);
        await tableClient.CreateIfNotExistsAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Should_append_and_read_single_event()
    {
        // Arrange
        var streamId = $"stream-{_testId}-single";
        var document = CreateObjectDocument(streamId);

        var eventToAppend = new JsonEvent
        {
            EventType = "OrderCreated",
            EventVersion = 0,
            Payload = """{"orderId":"ORD-001","customer":"Alice"}"""
        };

        // Act
        await _dataStore!.AppendAsync(document, eventToAppend);
        var events = (await _dataStore.ReadAsync(document))?.ToList();

        // Assert
        Assert.NotNull(events);
        Assert.Single(events);
        Assert.Equal("OrderCreated", events[0].EventType);
        Assert.Equal(0, events[0].EventVersion);
    }

    [Fact]
    public async Task Should_append_and_read_multiple_events()
    {
        // Arrange
        var streamId = $"stream-{_testId}-multi";
        var document = CreateObjectDocument(streamId);

        // Act - append first event
        await _dataStore!.AppendAsync(document, new JsonEvent
        {
            EventType = "OrderCreated",
            EventVersion = 0,
            Payload = """{"orderId":"ORD-002"}"""
        });

        // Append more events
        await _dataStore.AppendAsync(document, new JsonEvent
        {
            EventType = "OrderItemAdded",
            EventVersion = 1,
            Payload = """{"product":"Widget","quantity":5}"""
        });

        await _dataStore.AppendAsync(document, new JsonEvent
        {
            EventType = "OrderShipped",
            EventVersion = 2,
            Payload = """{"trackingNumber":"TRK-123"}"""
        });

        var events = (await _dataStore.ReadAsync(document))?.ToList();

        // Assert
        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
        Assert.Equal("OrderCreated", events[0].EventType);
        Assert.Equal("OrderItemAdded", events[1].EventType);
        Assert.Equal("OrderShipped", events[2].EventType);
    }

    [Fact]
    public async Task Should_read_events_from_specific_version()
    {
        // Arrange
        var streamId = $"stream-{_testId}-fromver";
        var document = CreateObjectDocument(streamId);

        for (int i = 0; i < 5; i++)
        {
            await _dataStore!.AppendAsync(document, new JsonEvent
            {
                EventType = $"Event{i}",
                EventVersion = i,
                Payload = $$$"""{"index":{{{i}}}}"""
            });
        }

        // Act - read starting from version 2
        var events = (await _dataStore!.ReadAsync(document, startVersion: 2))?.ToList();

        // Assert
        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
        Assert.Equal(2, events[0].EventVersion);
        Assert.Equal(3, events[1].EventVersion);
        Assert.Equal(4, events[2].EventVersion);
    }

    [Fact]
    public async Task Should_read_events_up_to_specific_version()
    {
        // Arrange
        var streamId = $"stream-{_testId}-untilver";
        var document = CreateObjectDocument(streamId);

        for (int i = 0; i < 5; i++)
        {
            await _dataStore!.AppendAsync(document, new JsonEvent
            {
                EventType = $"Event{i}",
                EventVersion = i,
                Payload = $$$"""{"index":{{{i}}}}"""
            });
        }

        // Act - read up to version 2 (inclusive)
        var events = (await _dataStore!.ReadAsync(document, untilVersion: 2))?.ToList();

        // Assert
        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
        Assert.Equal(0, events[0].EventVersion);
        Assert.Equal(1, events[1].EventVersion);
        Assert.Equal(2, events[2].EventVersion);
    }

    [Fact]
    public async Task Should_read_events_in_version_range()
    {
        // Arrange
        var streamId = $"stream-{_testId}-range";
        var document = CreateObjectDocument(streamId);

        for (int i = 0; i < 10; i++)
        {
            await _dataStore!.AppendAsync(document, new JsonEvent
            {
                EventType = $"Event{i}",
                EventVersion = i,
                Payload = $$$"""{"index":{{{i}}}}"""
            });
        }

        // Act - read versions 3 to 6 (inclusive)
        var events = (await _dataStore!.ReadAsync(document, startVersion: 3, untilVersion: 6))?.ToList();

        // Assert
        Assert.NotNull(events);
        Assert.Equal(4, events.Count);
        Assert.Equal(3, events[0].EventVersion);
        Assert.Equal(6, events[3].EventVersion);
    }

    [Fact]
    public async Task Should_return_null_for_nonexistent_stream()
    {
        // Arrange
        var streamId = $"stream-{_testId}-nonexistent";
        var document = CreateObjectDocument(streamId);

        // Act
        var events = await _dataStore!.ReadAsync(document);

        // Assert
        Assert.Null(events);
    }

    [Fact]
    public async Task Should_append_with_preserve_timestamp_flag()
    {
        // Arrange - Table Storage manages timestamps automatically, so preserveTimestamp
        // doesn't have the same effect as Blob storage. This test verifies the API works.
        var streamId = $"stream-{_testId}-timestamp";
        var document = CreateObjectDocument(streamId);

        var eventToAppend = new JsonEvent
        {
            EventType = "HistoricalEvent",
            EventVersion = 0,
            Payload = """{"data":"historical"}"""
        };

        // Act - append with preserveTimestamp = true (API compatibility)
        await _dataStore!.AppendAsync(document, preserveTimestamp: true, eventToAppend);
        var events = (await _dataStore.ReadAsync(document))?.ToList();

        // Assert
        Assert.NotNull(events);
        Assert.Single(events);
        Assert.Equal("HistoricalEvent", events[0].EventType);
    }

    [Fact]
    public async Task Should_reject_append_to_closed_stream()
    {
        // Arrange
        var streamId = $"stream-{_testId}-closed";
        var document = CreateObjectDocument(streamId);

        // Append some events first
        await _dataStore!.AppendAsync(document, new JsonEvent
        {
            EventType = "SomeEvent",
            EventVersion = 0,
            Payload = """{"data":"test"}"""
        });

        // Close the stream
        await _dataStore.AppendAsync(document, new JsonEvent
        {
            EventType = "EventStream.Closed",
            EventVersion = 1,
            Payload = """{"reason":"Test closure"}"""
        });

        // Act & Assert - try to append after closure
        await Assert.ThrowsAsync<EventStreamClosedException>(async () =>
        {
            await _dataStore.AppendAsync(document, new JsonEvent
            {
                EventType = "AfterCloseEvent",
                EventVersion = 2,
                Payload = """{"should":"fail"}"""
            });
        });
    }

    [Fact]
    public async Task Should_handle_large_payloads()
    {
        // Arrange
        var streamId = $"stream-{_testId}-large";
        var document = CreateObjectDocument(streamId);
        var largePayload = new string('x', 30000); // 30KB payload (Table Storage entity limit is 1MB)

        var largeEvent = new JsonEvent
        {
            EventType = "LargePayloadEvent",
            EventVersion = 0,
            Payload = $$$"""{"data":"{{{largePayload}}}"}"""
        };

        // Act
        await _dataStore!.AppendAsync(document, largeEvent);
        var events = (await _dataStore.ReadAsync(document))?.ToList();

        // Assert
        Assert.NotNull(events);
        Assert.Single(events);
        Assert.Contains(largePayload, events[0].Payload);
    }

    [Fact]
    public async Task Should_handle_batch_append()
    {
        // Arrange
        var streamId = $"stream-{_testId}-batch";
        var document = CreateObjectDocument(streamId);

        var events = Enumerable.Range(0, 10)
            .Select(i => new JsonEvent
            {
                EventType = $"BatchEvent{i}",
                EventVersion = i,
                Payload = $$$"""{"index":{{{i}}}}"""
            })
            .ToArray();

        // Act - append all events at once
        await _dataStore!.AppendAsync(document, events);
        var readEvents = (await _dataStore.ReadAsync(document))?.ToList();

        // Assert
        Assert.NotNull(readEvents);
        Assert.Equal(10, readEvents.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"BatchEvent{i}", readEvents[i].EventType);
            Assert.Equal(i, readEvents[i].EventVersion);
        }
    }

    [Fact]
    public async Task Should_handle_large_batch_append()
    {
        // Arrange - Table Storage batch limit is 100 entities
        var streamId = $"stream-{_testId}-largebatch";
        var document = CreateObjectDocument(streamId);

        var events = Enumerable.Range(0, 150)
            .Select(i => new JsonEvent
            {
                EventType = $"LargeBatchEvent{i}",
                EventVersion = i,
                Payload = $$$"""{"index":{{{i}}}}"""
            })
            .ToArray();

        // Act - append all events at once (should handle batching internally)
        await _dataStore!.AppendAsync(document, events);
        var readEvents = (await _dataStore.ReadAsync(document))?.ToList();

        // Assert
        Assert.NotNull(readEvents);
        Assert.Equal(150, readEvents.Count);
    }

    [Fact]
    public async Task Should_throw_when_no_events_provided()
    {
        // Arrange
        var streamId = $"stream-{_testId}-empty";
        var document = CreateObjectDocument(streamId);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _dataStore!.AppendAsync(document, Array.Empty<IEvent>());
        });
    }

    [Fact]
    public async Task Should_preserve_event_order()
    {
        // Arrange
        var streamId = $"stream-{_testId}-order";
        var document = CreateObjectDocument(streamId);

        var eventTypes = new[] { "First", "Second", "Third", "Fourth", "Fifth" };
        for (int i = 0; i < eventTypes.Length; i++)
        {
            await _dataStore!.AppendAsync(document, new JsonEvent
            {
                EventType = eventTypes[i],
                EventVersion = i,
                Payload = $$$"""{"seq":{{{i}}}}"""
            });
        }

        // Act
        var events = (await _dataStore!.ReadAsync(document))?.ToList();

        // Assert
        Assert.NotNull(events);
        for (int i = 0; i < eventTypes.Length; i++)
        {
            Assert.Equal(eventTypes[i], events[i].EventType);
            Assert.Equal(i, events[i].EventVersion);
        }
    }

    private IObjectDocument CreateObjectDocument(string streamId)
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            StreamType = "table",
            CurrentStreamVersion = -1,
            DataStore = "default",
            DocumentStore = "default"
        };

        var objectDocument = Substitute.For<IObjectDocument>();
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns(streamId);
        objectDocument.Active.Returns(streamInfo);
        objectDocument.TerminatedStreams.Returns(new List<TerminatedStream>());
        objectDocument.Hash.Returns("*");

        return objectDocument;
    }

    private static IAzureClientFactory<TableServiceClient> CreateTableClientFactory(TableServiceClient client)
    {
        var factory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
        factory.CreateClient(Arg.Any<string>()).Returns(client);
        return factory;
    }
}
