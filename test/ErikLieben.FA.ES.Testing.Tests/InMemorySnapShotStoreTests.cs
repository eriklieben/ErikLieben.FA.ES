#pragma warning disable CS0618 // Type or member is obsolete - tests verify behavior of legacy properties
#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
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
    public async Task Get_should_return_null_when_snapshot_missing()
    {
        // Arrange
        var store = new InMemorySnapShotStore();
        var doc = CreateDoc();

        // Act
        var result = await store.GetAsync(TypeInfo, doc, 1);

        // Assert
        Assert.Null(result);
    }
}
