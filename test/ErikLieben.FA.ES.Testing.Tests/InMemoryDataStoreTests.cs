#pragma warning disable CS0618 // Type or member is obsolete - testing deprecated API intentionally

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
        public int SchemaVersion { get; set; } = 1;
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
        await store.AppendAsync(document, default, e1, e2);
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
        await store.AppendAsync(document, default, e0, e1);
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
        await store.AppendAsync(document, default, new TestEvent { EventVersion = 0 }, new TestEvent { EventVersion = 1 });
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

    #region RemoveEventsForFailedCommitAsync Tests

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_remove_events_in_version_range()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        var e0 = new TestEvent { EventType = "E0", EventVersion = 0 };
        var e1 = new TestEvent { EventType = "E1", EventVersion = 1 };
        var e2 = new TestEvent { EventType = "E2", EventVersion = 2 };
        var e3 = new TestEvent { EventType = "E3", EventVersion = 3 };
        await store.AppendAsync(document, default, e0, e1, e2, e3);

        // Act - remove versions 1 and 2
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 1, 2);

        // Assert
        Assert.Equal(2, removed);
        var remaining = await store.ReadAsync(document);
        Assert.NotNull(remaining);
        var list = remaining!.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("E0", list[0].EventType);
        Assert.Equal("E3", list[1].EventType);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_return_zero_when_no_events_exist()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        // Act
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 0, 5);

        // Assert
        Assert.Equal(0, removed);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_return_zero_when_versions_not_found()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        var e0 = new TestEvent { EventType = "E0", EventVersion = 0 };
        await store.AppendAsync(document, default, e0);

        // Act - try to remove non-existent versions
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 5, 10);

        // Assert
        Assert.Equal(0, removed);
        var remaining = await store.ReadAsync(document);
        Assert.Single(remaining!);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_handle_partial_version_range()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        var e0 = new TestEvent { EventType = "E0", EventVersion = 0 };
        var e2 = new TestEvent { EventType = "E2", EventVersion = 2 };
        await store.AppendAsync(document, default, e0, e2);

        // Act - remove range 0-3 (only 0 and 2 exist)
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 0, 3);

        // Assert
        Assert.Equal(2, removed);
        var remaining = await store.ReadAsync(document);
        Assert.Empty(remaining!);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_handle_single_version()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        var e0 = new TestEvent { EventType = "E0", EventVersion = 0 };
        var e1 = new TestEvent { EventType = "E1", EventVersion = 1 };
        await store.AppendAsync(document, default, e0, e1);

        // Act - remove only version 1
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 1, 1);

        // Assert
        Assert.Equal(1, removed);
        var remaining = await store.ReadAsync(document);
        Assert.Single(remaining!);
        Assert.Equal("E0", remaining!.First().EventType);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_be_idempotent()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        var e0 = new TestEvent { EventType = "E0", EventVersion = 0 };
        var e1 = new TestEvent { EventType = "E1", EventVersion = 1 };
        await store.AppendAsync(document, default, e0, e1);

        // Act - remove version 1 twice
        var removed1 = await store.RemoveEventsForFailedCommitAsync(document, 1, 1);
        var removed2 = await store.RemoveEventsForFailedCommitAsync(document, 1, 1);

        // Assert
        Assert.Equal(1, removed1);
        Assert.Equal(0, removed2); // Already removed
        var remaining = await store.ReadAsync(document);
        Assert.Single(remaining!);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_preserve_events_outside_range()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        for (int i = 0; i < 10; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventType = $"E{i}", EventVersion = i });
        }

        // Act - remove middle range (3-6)
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 3, 6);

        // Assert
        Assert.Equal(4, removed);
        var remaining = await store.ReadAsync(document);
        var list = remaining!.ToList();
        Assert.Equal(6, list.Count);
        // Should have 0, 1, 2, 7, 8, 9
        Assert.Equal("E0", list[0].EventType);
        Assert.Equal("E1", list[1].EventType);
        Assert.Equal("E2", list[2].EventType);
        Assert.Equal("E7", list[3].EventType);
        Assert.Equal("E8", list[4].EventType);
        Assert.Equal("E9", list[5].EventType);
    }

    #endregion
}
