using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresSnapShotStoreTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private PostgresDocumentStore documentStore = null!;
    private PostgresSnapShotStore snapshotStore = null!;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        var typeSettings = new EventStreamDefaultTypeSettings("postgres");
        var tagFactory = new PostgresTagFactory(fixture.DataSource);
        documentStore = new PostgresDocumentStore(
            fixture.DataSource, fixture.Settings, typeSettings, tagFactory, fixture.Bootstrapper);
        snapshotStore = new PostgresSnapShotStore(fixture.DataSource);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Set_then_Get_round_trips_snapshot()
    {
        var doc = await documentStore.CreateAsync("Order", "snap-1");
        var snap = new SnapAggregate { Name = "hello", Count = 42 };

        await snapshotStore.SetAsync(snap, SnapJsonContext.Default.SnapAggregate, doc, version: 3);

        var loaded = await snapshotStore.GetAsync(SnapJsonContext.Default.SnapAggregate, doc, version: 3);
        Assert.NotNull(loaded);
        Assert.Equal("hello", loaded!.Name);
        Assert.Equal(42, loaded.Count);
    }

    [Fact]
    public async Task ListSnapshots_returns_all_versions_descending()
    {
        var doc = await documentStore.CreateAsync("Order", "snap-2");
        await snapshotStore.SetAsync(new SnapAggregate { Name = "v0" }, SnapJsonContext.Default.SnapAggregate, doc, 0);
        await snapshotStore.SetAsync(new SnapAggregate { Name = "v5" }, SnapJsonContext.Default.SnapAggregate, doc, 5);
        await snapshotStore.SetAsync(new SnapAggregate { Name = "v2" }, SnapJsonContext.Default.SnapAggregate, doc, 2);

        var list = await snapshotStore.ListSnapshotsAsync(doc);
        Assert.Equal(3, list.Count);
        Assert.Equal(5, list[0].Version);
        Assert.Equal(2, list[1].Version);
        Assert.Equal(0, list[2].Version);
    }

    [Fact]
    public async Task DeleteAsync_removes_targeted_snapshot()
    {
        var doc = await documentStore.CreateAsync("Order", "snap-3");
        await snapshotStore.SetAsync(new SnapAggregate { Name = "v1" }, SnapJsonContext.Default.SnapAggregate, doc, 1);

        var deleted = await snapshotStore.DeleteAsync(doc, version: 1);
        Assert.True(deleted);

        var list = await snapshotStore.ListSnapshotsAsync(doc);
        Assert.Empty(list);
    }
}

internal sealed class SnapAggregate : IBase
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }

    public Task Fold() => Task.CompletedTask;
    public void Fold(IEvent @event) { }
    public void ProcessSnapshot(object snapshot) { }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SnapAggregate))]
internal partial class SnapJsonContext : JsonSerializerContext
{
}
