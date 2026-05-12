using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresTagStoreTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private PostgresDocumentStore documentStore = null!;
    private PostgresDocumentTagStore documentTags = null!;
    private PostgresStreamTagStore streamTags = null!;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        var typeSettings = new EventStreamDefaultTypeSettings("postgres");
        var tagFactory = new PostgresTagFactory(fixture.DataSource);
        documentStore = new PostgresDocumentStore(
            fixture.DataSource, fixture.Settings, typeSettings, tagFactory, fixture.Bootstrapper);
        documentTags = new PostgresDocumentTagStore(fixture.DataSource);
        streamTags   = new PostgresStreamTagStore(fixture.DataSource);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Document_tags_are_set_and_retrievable()
    {
        var doc = await documentStore.CreateAsync("Order", "tagged-1");
        await documentTags.SetAsync(doc, "A");
        await documentTags.SetAsync(doc, "B");
        // Idempotent — setting same tag again should not duplicate.
        await documentTags.SetAsync(doc, "A");

        var matches = (await documentTags.GetAsync("Order", "A")).ToList();
        Assert.Single(matches);
        Assert.Equal("tagged-1", matches[0]);

        var matchesB = (await documentTags.GetAsync("Order", "B")).ToList();
        Assert.Single(matchesB);
    }

    [Fact]
    public async Task Document_tag_remove_unhooks_aggregate()
    {
        var doc = await documentStore.CreateAsync("Order", "tagged-2");
        await documentTags.SetAsync(doc, "Z");

        await documentTags.RemoveAsync(doc, "Z");
        var matches = (await documentTags.GetAsync("Order", "Z")).ToList();
        Assert.Empty(matches);
    }

    [Fact]
    public async Task Stream_tags_are_scoped_per_stream_identifier()
    {
        // Stream 1: tags ABC and DEF; stream 2: untagged.
        var doc = await documentStore.CreateAsync("Order", "tagged-3");

        var stream1Id = doc.Active.StreamIdentifier;
        await streamTags.SetAsync(doc, "ABC");
        await streamTags.SetAsync(doc, "DEF");

        // Simulate a second stream on the same aggregate by mutating the active stream identifier.
        // The data row stays the same; stream_tags is keyed by stream id so the new stream
        // starts with no tags by construction.
        var doc2 = await documentStore.GetAsync("Order", "tagged-3");
        doc2.Active.StreamIdentifier = stream1Id + "-rolled";

        // Stream 2 has no tags.
        var stream2HitsAbc = (await streamTags.GetAsync("Order", "ABC")).ToList();
        // Lookup is at the aggregate level, so the aggregate still appears via stream 1's tags.
        Assert.Contains("tagged-3", stream2HitsAbc);

        // Tagging the new stream with a different tag does not bleed into stream 1's tag set.
        await streamTags.SetAsync(doc2, "XYZ");

        var hitsXyz = (await streamTags.GetAsync("Order", "XYZ")).ToList();
        Assert.Contains("tagged-3", hitsXyz);

        // Direct row check: stream_tags has two distinct keys with distinct arrays.
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT stream_tags::text FROM faes_documents WHERE object_name='Order' AND object_id='tagged-3'",
            connection);
        var raw = (string)(await cmd.ExecuteScalarAsync())!;
        Assert.Contains("ABC", raw);
        Assert.Contains("DEF", raw);
        Assert.Contains("XYZ", raw);
        Assert.Contains(stream1Id, raw);
        Assert.Contains(stream1Id + "-rolled", raw);
    }

    [Fact]
    public async Task Stream_tag_remove_only_clears_the_specific_stream_entry()
    {
        var doc = await documentStore.CreateAsync("Order", "tagged-4");
        var stream1Id = doc.Active.StreamIdentifier;
        await streamTags.SetAsync(doc, "T1");

        var doc2 = await documentStore.GetAsync("Order", "tagged-4");
        doc2.Active.StreamIdentifier = stream1Id + "-rolled";
        await streamTags.SetAsync(doc2, "T1");

        // Remove from stream 1 only.
        await streamTags.RemoveAsync(doc, "T1");

        // The aggregate is still tagged via stream 2.
        var hits = (await streamTags.GetAsync("Order", "T1")).ToList();
        Assert.Contains("tagged-4", hits);
    }
}
