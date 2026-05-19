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

    [Fact]
    public async Task Append_refreshes_document_prev_hash_so_subsequent_SetAsync_succeeds()
    {
        // Regression: faes_append's UPDATE on faes_documents bumps xmin, but PostgresDataStore
        // used to leave document.PrevHash stuck at the value captured by the preceding SetAsync.
        // A second SetAsync in the same logical commit then hit a spurious xmin-mismatch.
        // This mirrors the sequence used by EventStream.LeasedSession.CommitWithoutChunkingAsync
        // for back-to-back commits on a single aggregate (e.g. AssignRole → EnableFeatureFlag).
        var doc = await documentStore.CreateAsync("Order", "order-occ-refresh");
        var hashAfterCreate = doc.Hash;

        // First commit: SetAsync (advances xmin) → AppendAsync (also advances xmin via the
        // UPDATE inside faes_append). After this pair, doc.PrevHash must reflect the
        // post-AppendAsync xmin, not the post-SetAsync one.
        doc.Active.CurrentStreamVersion = 0;
        await documentStore.SetAsync(doc);
        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "Created", EventVersion = 0, Payload = "{}" });

        Assert.NotEqual(hashAfterCreate, doc.PrevHash);

        // Second commit on the same in-memory document — would have thrown
        // PostgresProcessingException("xmin mismatch") before the fix.
        doc.Active.CurrentStreamVersion = 1;
        await documentStore.SetAsync(doc);
        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "Shipped", EventVersion = 1, Payload = "{}" });

        var events = (await dataStore.ReadAsync(doc))!.ToList();
        Assert.Equal(2, events.Count);
        Assert.Equal("Created", events[0].EventType);
        Assert.Equal("Shipped", events[1].EventType);
    }

    [Fact]
    public async Task Append_refreshes_PrevHash_to_value_visible_to_a_fresh_GetAsync()
    {
        // Consistency check: after AppendAsync the in-memory PrevHash must equal the xmin
        // that a brand-new GetAsync (i.e. a separate transaction) sees. Anything else means
        // the next SetAsync would have a 50/50 chance of failing OCC even on a sequential
        // single-writer workload.
        var doc = await documentStore.CreateAsync("Order", "order-occ-consistency");

        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "Created", EventVersion = 0, Payload = "{}" });

        var reloaded = await documentStore.GetAsync("Order", "order-occ-consistency");
        Assert.Equal(reloaded.Hash, doc.PrevHash);
        Assert.Equal(reloaded.PrevHash, doc.PrevHash);
    }

    [Fact]
    public async Task Multiple_appends_without_SetAsync_in_between_each_advance_PrevHash()
    {
        // Some flows append several batches back-to-back without an explicit SetAsync between
        // them (e.g. a single Session that buffers events across chunk boundaries). Each
        // AppendAsync must refresh PrevHash; otherwise the second batch would still hold
        // the xmin from before the first batch.
        var doc = await documentStore.CreateAsync("Order", "order-occ-multi-append");
        var seen = new List<string?> { doc.PrevHash };

        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "A", EventVersion = 0, Payload = "{}" });
        seen.Add(doc.PrevHash);

        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "B", EventVersion = 1, Payload = "{}" });
        seen.Add(doc.PrevHash);

        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "C", EventVersion = 2, Payload = "{}" });
        seen.Add(doc.PrevHash);

        // Each Append must produce a distinct PrevHash (Postgres assigns a new XID per tx).
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }
}
