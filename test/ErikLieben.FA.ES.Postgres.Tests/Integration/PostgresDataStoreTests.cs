using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Postgres.Exceptions;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresDataStoreTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private PostgresDataStore dataStore = null!;
    private PostgresDocumentStore documentStore = null!;
    private readonly EventStreamDefaultTypeSettings typeSettings = new("postgres");

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        var tagFactory = new PostgresTagFactory(fixture.DataSource);
        documentStore = new PostgresDocumentStore(
            fixture.DataSource, fixture.Settings, typeSettings, tagFactory, fixture.Bootstrapper);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Append_then_read_round_trips_events()
    {
        var doc = await documentStore.CreateAsync("Order", "order-1");

        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "Created", EventVersion = 0, Payload = """{"sku":"abc"}""" },
            new JsonEvent { EventType = "Shipped", EventVersion = 1, Payload = """{"to":"NL"}""" });

        var events = (await dataStore.ReadAsync(doc))!.ToList();
        Assert.Equal(2, events.Count);
        Assert.Equal("Created", events[0].EventType);
        Assert.Equal(0, events[0].EventVersion);
        Assert.Contains("\"sku\"", events[0].Payload);
        Assert.Contains("\"abc\"", events[0].Payload);
        Assert.Equal("Shipped", events[1].EventType);
        Assert.Equal(1, events[1].EventVersion);
    }

    [Fact]
    public async Task Append_with_stale_expected_version_throws_concurrency_exception()
    {
        var doc = await documentStore.CreateAsync("Order", "order-2");

        // First write succeeds; doc.Active.CurrentStreamVersion advances to 0.
        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "Created", EventVersion = 0, Payload = "{}" });

        // Simulate a stale in-memory document: rewind expected version to -1.
        doc.Active.CurrentStreamVersion = -1;

        await Assert.ThrowsAsync<PostgresProcessingException>(() =>
            dataStore.AppendAsync(doc, CancellationToken.None,
                new JsonEvent { EventType = "Created", EventVersion = 0, Payload = "{}" }));
    }

    [Fact]
    public async Task ReadAsStreamAsync_yields_events_in_version_order()
    {
        var doc = await documentStore.CreateAsync("Order", "order-3");
        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "A", EventVersion = 0, Payload = "{}" },
            new JsonEvent { EventType = "B", EventVersion = 1, Payload = "{}" },
            new JsonEvent { EventType = "C", EventVersion = 2, Payload = "{}" });

        var seen = new List<string>();
        await foreach (var e in dataStore.ReadAsStreamAsync(doc))
        {
            seen.Add(e.EventType);
        }
        Assert.Equal(new[] { "A", "B", "C" }, seen);
    }

    [Fact]
    public async Task Read_with_version_range_filters_events()
    {
        var doc = await documentStore.CreateAsync("Order", "order-4");
        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "A", EventVersion = 0, Payload = "{}" },
            new JsonEvent { EventType = "B", EventVersion = 1, Payload = "{}" },
            new JsonEvent { EventType = "C", EventVersion = 2, Payload = "{}" });

        var slice = (await dataStore.ReadAsync(doc, startVersion: 1, untilVersion: 1))!.ToList();
        Assert.Single(slice);
        Assert.Equal("B", slice[0].EventType);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommit_deletes_range()
    {
        var doc = await documentStore.CreateAsync("Order", "order-5");
        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "A", EventVersion = 0, Payload = "{}" },
            new JsonEvent { EventType = "B", EventVersion = 1, Payload = "{}" });

        var removed = await dataStore.RemoveEventsForFailedCommitAsync(doc, 1, 1);
        Assert.Equal(1, removed);

        var remaining = (await dataStore.ReadAsync(doc))!.ToList();
        Assert.Single(remaining);
        Assert.Equal("A", remaining[0].EventType);
    }
}
