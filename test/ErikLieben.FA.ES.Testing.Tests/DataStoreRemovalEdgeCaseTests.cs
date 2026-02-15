using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

/// <summary>
/// Edge case tests for DataStore removal operations to ensure
/// streams never reach a bad state after cleanup operations.
/// </summary>
public class DataStoreRemovalEdgeCaseTests
{
    #region Test Helpers

    private sealed class TestEvent : IEvent
    {
        public string EventType { get; set; } = "Test";
        public int EventVersion { get; set; }
        public int SchemaVersion { get; set; } = 1;
        public string? ExternalSequencer { get; } = null;
        public ActionMetadata? ActionMetadata { get; } = null;
        public Dictionary<string, string> Metadata { get; } = [];
        public string? Payload { get; set; }
    }

    private static InMemoryEventStreamDocument CreateDoc(string id = "42", string name = "TestAggregate")
    {
        var streamInfo = new StreamInformation
        {
            StreamConnectionName = "inMemory",
            SnapShotConnectionName = "inMemory",
            DocumentTagConnectionName = "inMemory",
            StreamTagConnectionName = "inMemory",
            StreamIdentifier = $"{id.Replace("-", string.Empty)}-0000000000",
            StreamType = "inMemory",
            DocumentTagType = "inMemory",
            CurrentStreamVersion = -1,
        };

        return new InMemoryEventStreamDocument(id, name, streamInfo, [], "1.0.0");
    }

    #endregion

    #region Idempotency Tests

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_be_idempotent()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        // Add events 0-4
        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Act - remove same range twice
        var firstRemoval = await store.RemoveEventsForFailedCommitAsync(document, 2, 4);
        var secondRemoval = await store.RemoveEventsForFailedCommitAsync(document, 2, 4);

        // Assert
        Assert.Equal(3, firstRemoval);
        Assert.Equal(0, secondRemoval); // Already removed

        var remaining = await store.ReadAsync(document);
        Assert.Equal(2, remaining!.Count());
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_multiple_overlapping_ranges_should_work()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        // Add events 0-9
        for (int i = 0; i < 10; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Act - remove overlapping ranges
        var first = await store.RemoveEventsForFailedCommitAsync(document, 3, 6);
        var second = await store.RemoveEventsForFailedCommitAsync(document, 4, 8); // Overlaps with first

        // Assert
        Assert.Equal(4, first); // Removed 3, 4, 5, 6
        Assert.Equal(2, second); // Only 7, 8 remain to remove (4, 5, 6 already gone)

        var remaining = await store.ReadAsync(document);
        Assert.Equal(4, remaining!.Count()); // 0, 1, 2, 9
    }

    #endregion

    #region Boundary Conditions

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_handle_empty_stream()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();
        // No events added

        // Act
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 0, 10);

        // Assert
        Assert.Equal(0, removed);

        var events = await store.ReadAsync(document);
        Assert.Empty(events!);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_handle_range_beyond_stream()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        // Add only 3 events (0, 1, 2)
        for (int i = 0; i < 3; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Act - try to remove range that goes beyond stream
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 1, 10);

        // Assert - only removes what exists
        Assert.Equal(2, removed); // Events 1 and 2

        var remaining = await store.ReadAsync(document);
        Assert.Single(remaining!);
        Assert.Equal(0, remaining!.First().EventVersion);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_handle_range_beyond_stored_events()
    {
        // Arrange
        // Note: InMemoryDataStore stores events using sequential keys (0, 1, 2...)
        // regardless of EventVersion. This test verifies that removing beyond
        // the stored key range returns 0.
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        // Add only 3 events (stored at keys 0, 1, 2)
        for (int i = 0; i < 3; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Act - try to remove range beyond stored events
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 10, 20);

        // Assert - nothing to remove (no events at keys 10-20)
        Assert.Equal(0, removed);

        var remaining = await store.ReadAsync(document);
        Assert.Equal(3, remaining!.Count());
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_handle_single_version()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Act
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 2, 2);

        // Assert
        Assert.Equal(1, removed);

        var remaining = await store.ReadAsync(document);
        Assert.Equal(4, remaining!.Count());
        Assert.DoesNotContain(remaining!, e => e.EventVersion == 2);
    }

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_handle_all_events()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Act - remove all
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 0, 4);

        // Assert
        Assert.Equal(5, removed);

        var remaining = await store.ReadAsync(document);
        Assert.Empty(remaining!);
    }

    #endregion

    #region Version Gap Tests

    [Fact]
    public async Task Should_allow_reading_after_removal_creates_gap()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        // Add events 0-9
        for (int i = 0; i < 10; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i, Payload = $"Event{i}" });
        }

        // Act - remove middle section creating gap
        await store.RemoveEventsForFailedCommitAsync(document, 4, 6);

        // Assert - reading should still work (returns events with gap)
        var allEvents = await store.ReadAsync(document);
        Assert.Equal(7, allEvents!.Count()); // 0, 1, 2, 3, 7, 8, 9

        var versions = allEvents!.Select(e => e.EventVersion).ToList();
        Assert.Contains(3, versions);
        Assert.DoesNotContain(4, versions);
        Assert.DoesNotContain(5, versions);
        Assert.DoesNotContain(6, versions);
        Assert.Contains(7, versions);
    }

    [Fact]
    public async Task Should_allow_reading_specific_range_after_removal()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        for (int i = 0; i < 10; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Remove 4-6
        await store.RemoveEventsForFailedCommitAsync(document, 4, 6);

        // Act - read range that spans gap
        var events = await store.ReadAsync(document, 2, 8);

        // Assert - returns what exists in range
        Assert.NotNull(events);
        var versions = events!.Select(e => e.EventVersion).ToList();
        Assert.Contains(2, versions);
        Assert.Contains(3, versions);
        Assert.DoesNotContain(4, versions);
        Assert.DoesNotContain(5, versions);
        Assert.DoesNotContain(6, versions);
        Assert.Contains(7, versions);
        Assert.Contains(8, versions);
    }

    #endregion

    #region Concurrent Operations Simulation

    [Fact]
    public async Task Should_handle_append_after_removal()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        // Add events 0-4
        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Remove 3-4
        await store.RemoveEventsForFailedCommitAsync(document, 3, 4);

        // Act - append new events (simulating retry with same versions)
        await store.AppendAsync(document, default, new TestEvent { EventVersion = 3, Payload = "Retry3" });
        await store.AppendAsync(document, default, new TestEvent { EventVersion = 4, Payload = "Retry4" });
        await store.AppendAsync(document, default, new TestEvent { EventVersion = 5, Payload = "New5" });

        // Assert
        var events = await store.ReadAsync(document);
        Assert.Equal(6, events!.Count());

        var v3 = events!.FirstOrDefault(e => e.EventVersion == 3);
        Assert.NotNull(v3);
        Assert.Equal("Retry3", v3!.Payload);
    }

    [Fact]
    public async Task Should_handle_multiple_documents_independently()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var doc1 = CreateDoc("doc1");
        var doc2 = CreateDoc("doc2");

        // Add events to both
        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(doc1, default, new TestEvent { EventVersion = i });
            await store.AppendAsync(doc2, default, new TestEvent { EventVersion = i });
        }

        // Act - remove from doc1 only
        var removed = await store.RemoveEventsForFailedCommitAsync(doc1, 2, 4);

        // Assert - doc1 affected, doc2 unchanged
        Assert.Equal(3, removed);

        var doc1Events = await store.ReadAsync(doc1);
        var doc2Events = await store.ReadAsync(doc2);

        Assert.Equal(2, doc1Events!.Count());
        Assert.Equal(5, doc2Events!.Count());
    }

    #endregion

    #region Event Payload Preservation

    [Fact]
    public async Task Should_preserve_event_payloads_after_partial_removal()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(document, default, new TestEvent
            {
                EventVersion = i,
                EventType = $"Type{i}",
                Payload = $"{{\"data\": \"value{i}\"}}"
            });
        }

        // Act - remove middle
        await store.RemoveEventsForFailedCommitAsync(document, 1, 3);

        // Assert - remaining events have intact payloads
        var remaining = await store.ReadAsync(document);
        Assert.Equal(2, remaining!.Count());

        var event0 = remaining!.First(e => e.EventVersion == 0);
        var event4 = remaining!.First(e => e.EventVersion == 4);

        Assert.Equal("Type0", event0.EventType);
        Assert.Equal("{\"data\": \"value0\"}", event0.Payload);
        Assert.Equal("Type4", event4.EventType);
        Assert.Equal("{\"data\": \"value4\"}", event4.Payload);
    }

    #endregion

    #region Negative Range Tests

    [Fact]
    public async Task Should_handle_inverted_range_gracefully()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Act - from > to (inverted range)
        var removed = await store.RemoveEventsForFailedCommitAsync(document, 4, 1);

        // Assert - no events should be removed (invalid range)
        Assert.Equal(0, removed);

        var remaining = await store.ReadAsync(document);
        Assert.Equal(5, remaining!.Count());
    }

    [Fact]
    public async Task Should_handle_negative_version_numbers()
    {
        // Arrange
        var store = new InMemoryDataStore();
        var document = CreateDoc();

        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(document, default, new TestEvent { EventVersion = i });
        }

        // Act - negative versions shouldn't match anything
        var removed = await store.RemoveEventsForFailedCommitAsync(document, -5, -1);

        // Assert
        Assert.Equal(0, removed);

        var remaining = await store.ReadAsync(document);
        Assert.Equal(5, remaining!.Count());
    }

    #endregion
}
