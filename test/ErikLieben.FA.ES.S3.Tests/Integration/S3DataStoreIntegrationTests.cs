using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests.Integration;

[Collection("MinIO")]
[Trait("Category", "Integration")]
public class S3DataStoreIntegrationTests : IAsyncLifetime
{
    private readonly MinioContainerFixture _fixture;
    private readonly EventStreamS3Settings _settings;
    private S3DataStore _sut = null!;

    public S3DataStoreIntegrationTests(MinioContainerFixture fixture)
    {
        _fixture = fixture;
        // Use a unique bucket per test class to avoid interference
        _settings = fixture.CreateSettings(bucketName: $"datastore-{Guid.NewGuid():N}");
    }

    public Task InitializeAsync()
    {
        S3DataStore.ClearVerifiedBucketsCache();
        S3DataStore.ClearClosedStreamCache();
        var clientFactory = new S3ClientFactory(_settings);
        _sut = new S3DataStore(clientFactory, _settings);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        S3DataStore.ClearVerifiedBucketsCache();
        S3DataStore.ClearClosedStreamCache();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Should_append_and_read_events()
    {
        var document = CreateObjectDocument("stream-001");

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "TestEvent", EventVersion = 0, Payload = """{"message":"Hello"}""" },
            new JsonEvent { EventType = "TestEvent", EventVersion = 1, Payload = """{"message":"World"}""" });

        var readEvents = await _sut.ReadAsync(document);

        Assert.NotNull(readEvents);
        Assert.Equal(2, readEvents.Count());
    }

    [Fact]
    public async Task Should_read_events_with_start_version()
    {
        var document = CreateObjectDocument("stream-002");

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "Event1", EventVersion = 0, Payload = """{"seq":1}""" },
            new JsonEvent { EventType = "Event2", EventVersion = 1, Payload = """{"seq":2}""" },
            new JsonEvent { EventType = "Event3", EventVersion = 2, Payload = """{"seq":3}""" });

        var readEvents = await _sut.ReadAsync(document, startVersion: 1);

        Assert.NotNull(readEvents);
        Assert.Equal(2, readEvents.Count());
    }

    [Fact]
    public async Task Should_read_events_until_version()
    {
        var document = CreateObjectDocument("stream-003");

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "Event1", EventVersion = 0, Payload = """{"seq":1}""" },
            new JsonEvent { EventType = "Event2", EventVersion = 1, Payload = """{"seq":2}""" },
            new JsonEvent { EventType = "Event3", EventVersion = 2, Payload = """{"seq":3}""" });

        var readEvents = await _sut.ReadAsync(document, untilVersion: 1);

        Assert.NotNull(readEvents);
        Assert.Equal(2, readEvents.Count());
    }

    [Fact]
    public async Task Should_return_null_when_no_events_exist()
    {
        var document = CreateObjectDocument("stream-nonexistent");

        var readEvents = await _sut.ReadAsync(document);

        Assert.Null(readEvents);
    }

    [Fact]
    public async Task Should_append_multiple_batches()
    {
        var document = CreateObjectDocument("stream-004");

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "Batch1", EventVersion = 0, Payload = """{"batch":1}""" });

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "Batch2", EventVersion = 1, Payload = """{"batch":2}""" });

        var readEvents = (await _sut.ReadAsync(document))?.ToList();

        Assert.NotNull(readEvents);
        Assert.Equal(2, readEvents.Count);
        Assert.Equal("Batch1", readEvents[0].EventType);
        Assert.Equal("Batch2", readEvents[1].EventType);
    }

    [Fact]
    public async Task Should_preserve_event_order()
    {
        var document = CreateObjectDocument("stream-005");

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "First", EventVersion = 0, Payload = """{"order":1}""" },
            new JsonEvent { EventType = "Second", EventVersion = 1, Payload = """{"order":2}""" },
            new JsonEvent { EventType = "Third", EventVersion = 2, Payload = """{"order":3}""" });

        var readEvents = (await _sut.ReadAsync(document))?.ToList();

        Assert.NotNull(readEvents);
        Assert.Equal("First", readEvents[0].EventType);
        Assert.Equal("Second", readEvents[1].EventType);
        Assert.Equal("Third", readEvents[2].EventType);
    }

    [Fact]
    public async Task Should_preserve_event_versions()
    {
        var document = CreateObjectDocument("stream-006");

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "E1", EventVersion = 0, Payload = "{}" },
            new JsonEvent { EventType = "E2", EventVersion = 1, Payload = "{}" },
            new JsonEvent { EventType = "E3", EventVersion = 2, Payload = "{}" });

        var readEvents = (await _sut.ReadAsync(document))?.ToList();

        Assert.NotNull(readEvents);
        Assert.Equal(0, readEvents[0].EventVersion);
        Assert.Equal(1, readEvents[1].EventVersion);
        Assert.Equal(2, readEvents[2].EventVersion);
    }

    [Fact]
    public async Task Should_preserve_event_payload()
    {
        var document = CreateObjectDocument("stream-007");
        var payload = """{"name":"Test","value":42,"nested":{"key":"deep"}}""";

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "WithPayload", EventVersion = 0, Payload = payload });

        var readEvents = (await _sut.ReadAsync(document))?.ToList();

        Assert.NotNull(readEvents);
        Assert.Single(readEvents);
        Assert.Equal(payload, readEvents[0].Payload);
    }

    [Fact]
    public async Task Should_handle_large_batch_of_events()
    {
        var document = CreateObjectDocument("stream-large");
        var events = Enumerable.Range(0, 50)
            .Select(i => new JsonEvent
            {
                EventType = "BulkEvent",
                EventVersion = i,
                Payload = $$$"""{"index":{{{i}}}}"""
            })
            .ToArray();

        await _sut.AppendAsync(document, default, events);

        var readEvents = await _sut.ReadAsync(document);

        Assert.NotNull(readEvents);
        Assert.Equal(50, readEvents.Count());
    }

    [Fact]
    public async Task Should_read_as_stream_async()
    {
        var document = CreateObjectDocument("stream-streaming");

        await _sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "E1", EventVersion = 0, Payload = "{}" },
            new JsonEvent { EventType = "E2", EventVersion = 1, Payload = "{}" });

        var events = new List<IEvent>();
        await foreach (var evt in _sut.ReadAsStreamAsync(document))
        {
            events.Add(evt);
        }

        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task Should_auto_create_bucket()
    {
        // Use settings with a brand-new bucket name
        var settings = _fixture.CreateSettings(bucketName: $"auto-create-{Guid.NewGuid():N}");
        var clientFactory = new S3ClientFactory(settings);
        var sut = new S3DataStore(clientFactory, settings);
        S3DataStore.ClearVerifiedBucketsCache();

        var document = CreateObjectDocument("stream-autocreate", dataStore: "s3");

        await sut.AppendAsync(
            document,
            default,
            new JsonEvent { EventType = "Test", EventVersion = 0, Payload = "{}" });

        var readEvents = await sut.ReadAsync(document);
        Assert.NotNull(readEvents);
        Assert.Single(readEvents);
    }

    private static IObjectDocument CreateObjectDocument(
        string streamId,
        string objectName = "test",
        string dataStore = "s3")
    {
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            StreamType = "s3",
            CurrentStreamVersion = -1,
            DataStore = dataStore
        };

        var doc = Substitute.For<IObjectDocument>();
        doc.ObjectName.Returns(objectName);
        doc.ObjectId.Returns(streamId);
        doc.Active.Returns(streamInfo);
        doc.Hash.Returns((string?)null);
        doc.PrevHash.Returns((string?)null);
        doc.TerminatedStreams.Returns(new List<TerminatedStream>());
        doc.SchemaVersion.Returns((string?)null);
        return doc;
    }
}
