using ErikLieben.FA.ES;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Integration;

[Collection("CosmosDb")]
[Trait("Category", "Integration")]
public class CosmosDbDataStoreIntegrationTests : IAsyncLifetime
{
    private readonly CosmosDbContainerFixture _fixture;
    private readonly EventStreamCosmosDbSettings _settings;
    private Database? _database;

    public CosmosDbDataStoreIntegrationTests(CosmosDbContainerFixture fixture)
    {
        _fixture = fixture;
        _settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = $"testdb_{Guid.NewGuid():N}",
            EventsContainerName = "events",
            AutoCreateContainers = true
        };
    }

    public async Task InitializeAsync()
    {
        // Create a unique database for this test class
        _database = (await _fixture.CosmosClient!.CreateDatabaseIfNotExistsAsync(_settings.DatabaseName)).Database;
    }

    public async Task DisposeAsync()
    {
        // Clean up the database after tests
        if (_database != null)
        {
            try
            {
                await _database.DeleteAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Should_append_and_read_events()
    {
        // Arrange
        var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);

        var objectDocument = CreateObjectDocument("test-stream-001");
        var events = new IEvent[]
        {
            new JsonEvent { EventType = "TestEvent", EventVersion = 1, Payload = """{"message":"Hello"}""" },
            new JsonEvent { EventType = "TestEvent", EventVersion = 1, Payload = """{"message":"World"}""" }
        };

        // Act
        await sut.AppendAsync(objectDocument, events);

        // Assert
        var readEvents = await sut.ReadAsync(objectDocument);
        Assert.NotNull(readEvents);
        Assert.Equal(2, readEvents.Count());
    }

    [Fact]
    public async Task Should_read_events_with_start_version()
    {
        // Arrange
        var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);

        var objectDocument = CreateObjectDocument("test-stream-002");
        var events = new IEvent[]
        {
            new JsonEvent { EventType = "Event1", EventVersion = 1, Payload = """{"seq":1}""" },
            new JsonEvent { EventType = "Event2", EventVersion = 1, Payload = """{"seq":2}""" },
            new JsonEvent { EventType = "Event3", EventVersion = 1, Payload = """{"seq":3}""" }
        };

        await sut.AppendAsync(objectDocument, events);

        // Act - read from version 1 (skip first event at version 0)
        var readEvents = await sut.ReadAsync(objectDocument, startVersion: 1);

        // Assert
        Assert.NotNull(readEvents);
        Assert.Equal(2, readEvents.Count());
    }

    [Fact]
    public async Task Should_read_events_until_version()
    {
        // Arrange
        var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);

        var objectDocument = CreateObjectDocument("test-stream-003");
        var events = new IEvent[]
        {
            new JsonEvent { EventType = "Event1", EventVersion = 1, Payload = """{"seq":1}""" },
            new JsonEvent { EventType = "Event2", EventVersion = 1, Payload = """{"seq":2}""" },
            new JsonEvent { EventType = "Event3", EventVersion = 1, Payload = """{"seq":3}""" }
        };

        await sut.AppendAsync(objectDocument, events);

        // Act - read until version 1 (include first two events)
        var readEvents = await sut.ReadAsync(objectDocument, untilVersion: 1);

        // Assert
        Assert.NotNull(readEvents);
        Assert.Equal(2, readEvents.Count());
    }

    [Fact]
    public async Task Should_return_null_when_no_events_exist()
    {
        // Arrange
        var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);
        var objectDocument = CreateObjectDocument("test-stream-empty");

        // Act
        var readEvents = await sut.ReadAsync(objectDocument);

        // Assert
        Assert.Null(readEvents);
    }

    [Fact]
    public async Task Should_append_large_batch_of_events()
    {
        // Arrange
        var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);

        var objectDocument = CreateObjectDocument("test-stream-large");
        var events = Enumerable.Range(0, 50)
            .Select(i => (IEvent)new JsonEvent
            {
                EventType = "TestEvent",
                EventVersion = 1,
                Payload = $$$"""{"index":{{{i}}}}"""
            })
            .ToArray();

        // Act
        await sut.AppendAsync(objectDocument, events);

        // Assert
        var readEvents = await sut.ReadAsync(objectDocument);
        Assert.NotNull(readEvents);
        Assert.Equal(50, readEvents.Count());
    }

    [Fact]
    public async Task Should_preserve_event_order()
    {
        // Arrange
        var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);

        var objectDocument = CreateObjectDocument("test-stream-order");
        var events = new IEvent[]
        {
            new JsonEvent { EventType = "First", EventVersion = 1, Payload = """{"order":1}""" },
            new JsonEvent { EventType = "Second", EventVersion = 1, Payload = """{"order":2}""" },
            new JsonEvent { EventType = "Third", EventVersion = 1, Payload = """{"order":3}""" }
        };

        await sut.AppendAsync(objectDocument, events);

        // Act
        var readEvents = (await sut.ReadAsync(objectDocument))?.ToList();

        // Assert
        Assert.NotNull(readEvents);
        Assert.Equal("First", readEvents[0].EventType);
        Assert.Equal("Second", readEvents[1].EventType);
        Assert.Equal("Third", readEvents[2].EventType);
    }

    [Fact]
    public async Task Should_set_version_on_events()
    {
        // Arrange
        var sut = new CosmosDbDataStore(_fixture.CosmosClient!, _settings);

        var objectDocument = CreateObjectDocument("test-stream-versions");
        var events = new IEvent[]
        {
            new JsonEvent { EventType = "Event1", EventVersion = 1, Payload = "{}" },
            new JsonEvent { EventType = "Event2", EventVersion = 1, Payload = "{}" },
            new JsonEvent { EventType = "Event3", EventVersion = 1, Payload = "{}" }
        };

        await sut.AppendAsync(objectDocument, events);

        // Act
        var readEvents = (await sut.ReadAsync(objectDocument))?.ToList();

        // Assert - versions should be 0, 1, 2
        Assert.NotNull(readEvents);
        Assert.Equal(0, readEvents[0].EventVersion);
        Assert.Equal(1, readEvents[1].EventVersion);
        Assert.Equal(2, readEvents[2].EventVersion);
    }

    private static IObjectDocument CreateObjectDocument(string streamId)
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            StreamType = "cosmosdb",
            CurrentStreamVersion = -1,
            DataStore = "cosmosdb"
        };

        var objectDocument = Substitute.For<IObjectDocument>();
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns(streamId);
        objectDocument.Active.Returns(streamInfo);
        objectDocument.TerminatedStreams.Returns([]);

        return objectDocument;
    }
}
