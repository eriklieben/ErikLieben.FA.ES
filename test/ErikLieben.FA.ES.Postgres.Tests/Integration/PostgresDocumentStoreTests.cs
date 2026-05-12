using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Postgres.Exceptions;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresDocumentStoreTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private PostgresDocumentStore store = null!;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        var typeSettings = new EventStreamDefaultTypeSettings("postgres");
        var tagFactory = new PostgresTagFactory(fixture.DataSource);
        store = new PostgresDocumentStore(
            fixture.DataSource, fixture.Settings, typeSettings, tagFactory, fixture.Bootstrapper);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateAsync_returns_document_with_xmin_hash()
    {
        var doc = await store.CreateAsync("Order", "doc-1");
        Assert.Equal("Order", doc.ObjectName);
        Assert.Equal("doc-1", doc.ObjectId);
        Assert.False(string.IsNullOrEmpty(doc.Hash));
        Assert.Equal(doc.Hash, doc.PrevHash);
    }

    [Fact]
    public async Task CreateAsync_is_idempotent_on_second_call()
    {
        var first = await store.CreateAsync("Order", "doc-2");
        var second = await store.CreateAsync("Order", "doc-2");
        Assert.Equal(first.ObjectId, second.ObjectId);
        // Both reads return the same xmin (no UPDATE happened on the second call).
        Assert.Equal(first.Hash, second.Hash);
    }

    [Fact]
    public async Task SetAsync_updates_active_and_advances_xmin()
    {
        var doc = await store.CreateAsync("Order", "doc-3");
        var originalHash = doc.Hash;

        doc.Active.CurrentStreamVersion = 5;
        await store.SetAsync(doc);

        Assert.NotEqual(originalHash, doc.Hash);

        var reloaded = await store.GetAsync("Order", "doc-3");
        Assert.Equal(5, reloaded.Active.CurrentStreamVersion);
    }

    [Fact]
    public async Task SetAsync_with_stale_xmin_throws()
    {
        var docA = await store.CreateAsync("Order", "doc-4");
        var docB = await store.GetAsync("Order", "doc-4");

        docA.Active.CurrentStreamVersion = 1;
        await store.SetAsync(docA); // A wins; xmin advances.

        docB.Active.CurrentStreamVersion = 2;
        await Assert.ThrowsAsync<PostgresProcessingException>(() => store.SetAsync(docB));
    }
}
