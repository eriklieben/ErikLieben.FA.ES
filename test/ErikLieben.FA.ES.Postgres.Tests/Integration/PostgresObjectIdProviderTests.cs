using ErikLieben.FA.ES.Configuration;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresObjectIdProviderTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private PostgresDocumentStore documentStore = null!;
    private PostgresObjectIdProvider provider = null!;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        var typeSettings = new EventStreamDefaultTypeSettings("postgres");
        var tagFactory = new PostgresTagFactory(fixture.DataSource);
        documentStore = new PostgresDocumentStore(
            fixture.DataSource, fixture.Settings, typeSettings, tagFactory, fixture.Bootstrapper);
        provider = new PostgresObjectIdProvider(fixture.DataSource);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Exists_and_Count_work()
    {
        await documentStore.CreateAsync("Order", "id-1");
        await documentStore.CreateAsync("Order", "id-2");
        await documentStore.CreateAsync("Customer", "id-a");

        Assert.True(await provider.ExistsAsync("Order", "id-1"));
        Assert.False(await provider.ExistsAsync("Order", "id-x"));
        Assert.Equal(2, await provider.CountAsync("Order"));
        Assert.Equal(1, await provider.CountAsync("Customer"));
    }

    [Fact]
    public async Task GetObjectIds_paginates_with_continuation_token()
    {
        for (var i = 0; i < 5; i++)
        {
            await documentStore.CreateAsync("Order", $"id-{i:D3}");
        }

        var page1 = await provider.GetObjectIdsAsync("Order", null, pageSize: 2);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.HasNextPage);

        var page2 = await provider.GetObjectIdsAsync("Order", page1.ContinuationToken, pageSize: 2);
        Assert.Equal(2, page2.Items.Count);
        Assert.True(page2.HasNextPage);

        var page3 = await provider.GetObjectIdsAsync("Order", page2.ContinuationToken, pageSize: 2);
        Assert.Single(page3.Items);
        Assert.False(page3.HasNextPage);
    }
}
