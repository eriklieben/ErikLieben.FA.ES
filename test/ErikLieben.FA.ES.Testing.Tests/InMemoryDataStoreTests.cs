using System.Text.Json;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryDataStoreTests
{
    private sealed class TestEvent : IEvent
    {
        public string EventType { get; set; } = "Test";
        public int EventVersion { get; set; }
        public string? ExternalSequencer { get; } = string.Empty;
        public ActionMetadata? ActionMetadata { get; } = new();
        public Dictionary<string, string> Metadata { get; } = new();
        public string Payload { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.MinValue;
    }
    private static InMemoryEventStreamDocument CreateDoc(string name = "Order", string id = "42")
    {
        return new InMemoryEventStreamDocument(
            id,
            name,
            new StreamInformation
            {
                StreamConnectionName = "inMemory",
                SnapShotConnectionName = "inMemory",
                DocumentTagConnectionName = "inMemory",
                StreamTagConnectionName = "inMemory",
                StreamIdentifier = $"{id.Replace("-", string.Empty)}-0000000000",
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
                CurrentStreamVersion = -1,
            },
            [],
            "1.0.0");
    }

    [Fact]
    public async Task Append_and_Read_should_store_and_return_events_in_order()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        var e1 = new JsonEvent { EventType = "E1", EventVersion = 0, Payload = JsonSerializer.Serialize(new { a = 1 }) };
        var e2 = new JsonEvent { EventType = "E2", EventVersion = 1, Payload = JsonSerializer.Serialize(new { b = 2 }) };

        // Act
        await store.AppendAsync(document, e1, e2);
        var result = await store.ReadAsync(document);

        // Assert
        Assert.NotNull(result);
        var list = result!.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("E1", list[0].EventType);
        Assert.Equal("E2", list[1].EventType);
    }

    [Fact]
    public async Task Read_should_return_empty_list_when_no_events_are_present()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        // Act
        var result = await store.ReadAsync(document);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public async Task RemoveAsync_should_remove_events_by_version_and_noop_otherwise()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        var e0 = new TestEvent { EventType = "E0", EventVersion = 0 };
        var e1 = new TestEvent { EventType = "E1", EventVersion = 1 };
        await store.AppendAsync(document, e0, e1);
        var key = InMemoryDataStore.GetStoreKey(document.ObjectName, document.ObjectId);

        // Sanity
        var dict = store.GetDataStoreFor(key);
        Assert.Equal(2, dict.Count);

        // Act - remove version 0 and a non-existing version 5
        await store.RemoveAsync(document, new TestEvent { EventVersion = 0 }, new TestEvent { EventVersion = 5 });

        // Assert
        Assert.False(dict.ContainsKey(0));
        Assert.True(dict.ContainsKey(1));
        Assert.Single(dict);
    }

    [Fact]
    public async Task GetDataStoreFor_should_return_backing_dictionary()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        await store.AppendAsync(document, new TestEvent { EventVersion = 0 }, new TestEvent { EventVersion = 1 });
        var key = InMemoryDataStore.GetStoreKey(document.ObjectName, document.ObjectId);

        // Act
        var dict = store.GetDataStoreFor(key);

        // Assert
        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(0));
        Assert.True(dict.ContainsKey(1));
    }

    [Fact]
    public async Task Read_should_throw_when_document_is_null_or_stream_identifier_missing()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var invalidDoc = new InMemoryEventStreamDocument(
            "id",
            "order",
            new StreamInformation(),
            [],
            "1.0.0");

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.ReadAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync(invalidDoc));
    }

    [Fact]
    public void GetStoreKey_should_lowercase_objectName_and_include_id()
    {
        // Arrange
        var name = "CaSeD";
        var id = "ABC";

        // Act
        var key = InMemoryDataStore.GetStoreKey(name, id);

        // Assert
        Assert.Equal("cased__ABC", key);
    }
}
