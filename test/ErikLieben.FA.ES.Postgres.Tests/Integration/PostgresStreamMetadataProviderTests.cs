using ErikLieben.FA.ES.Configuration;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresStreamMetadataProviderTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private PostgresDataStore dataStore = null!;
    private PostgresDocumentStore documentStore = null!;
    private PostgresStreamMetadataProvider provider = null!;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        var typeSettings = new EventStreamDefaultTypeSettings("postgres");
        var tagFactory = new PostgresTagFactory(fixture.DataSource);
        documentStore = new PostgresDocumentStore(
            fixture.DataSource, fixture.Settings, typeSettings, tagFactory, fixture.Bootstrapper);
        dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        provider  = new PostgresStreamMetadataProvider(fixture.DataSource);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Returns_null_when_no_events()
    {
        var meta = await provider.GetStreamMetadataAsync("Order", "missing");
        Assert.Null(meta);
    }

    [Fact]
    public async Task Returns_count_and_date_range()
    {
        var doc = await documentStore.CreateAsync("Order", "meta-1");
        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "A", EventVersion = 0, Payload = "{}" },
            new JsonEvent { EventType = "B", EventVersion = 1, Payload = "{}" },
            new JsonEvent { EventType = "C", EventVersion = 2, Payload = "{}" });

        var meta = await provider.GetStreamMetadataAsync("Order", "meta-1");
        Assert.NotNull(meta);
        Assert.Equal("Order", meta!.ObjectName);
        Assert.Equal("meta-1", meta.ObjectId);
        Assert.Equal(3, meta.EventCount);
        Assert.NotNull(meta.OldestEventDate);
        Assert.NotNull(meta.NewestEventDate);
        Assert.True(meta.NewestEventDate >= meta.OldestEventDate);
    }
}
