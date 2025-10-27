using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemorySnapShotStoreTests
{
    private class Dummy : IBase { public int V { get; set; } public Task Fold() => Task.CompletedTask; public void Fold(IEvent @event) { } public void ProcessSnapshot(object snapshot) { } }

    private static InMemoryEventStreamDocument CreateDoc()
        => new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation
            {
                StreamConnectionName = "inMemory",
                SnapShotConnectionName = "inMemory",
                DocumentTagConnectionName = "inMemory",
                StreamTagConnectionName = "inMemory",
                StreamIdentifier = "1-0000000000",
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
                CurrentStreamVersion = -1,
            },
            [],
            "1.0.0");

    private static JsonTypeInfo<Dummy> TypeInfo => JsonTypeInfo.CreateJsonTypeInfo<Dummy>(new())!;

    [Fact]
    public async Task Set_and_Get_should_roundtrip_snapshot()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();
        var obj = new Dummy { V = 5 };

        // Act
        await store.SetAsync(obj, TypeInfo, doc, version: 10);
        var fetched = await store.GetAsync(TypeInfo, doc, version: 10);
        var fetchedGeneric = await store.GetAsync<Dummy>(TypeInfo, doc, version: 10);

        // Assert
        Assert.Same(obj, fetched);
        Assert.Same(obj, fetchedGeneric);
    }

    [Fact]
    public async Task Get_should_throw_when_snapshot_missing()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(async () => await store.GetAsync(TypeInfo, doc, 1));
    }
}
