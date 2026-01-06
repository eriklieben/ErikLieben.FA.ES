#pragma warning disable CS0618 // Type or member is obsolete - testing deprecated API intentionally

using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Events;
using ErikLieben.FA.ES.EventStreamManagement.Repair;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Repair;

/// <summary>
/// Tests for the StreamRepairService.
/// </summary>
public class StreamRepairServiceTests
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

    private static InMemoryEventStreamDocument CreateDoc(
        string name = "Order",
        string id = "42",
        bool isBroken = false,
        BrokenStreamInfo? brokenInfo = null)
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
            IsBroken = isBroken,
            BrokenInfo = brokenInfo
        };

        return new InMemoryEventStreamDocument(id, name, streamInfo, [], "1.0.0");
    }

    private static StreamRepairService CreateService(
        InMemoryDataStore dataStore,
        InMemoryDocumentStore documentStore)
    {
        var logger = NullLogger<StreamRepairService>.Instance;
        return new StreamRepairService(dataStore, documentStore, logger);
    }

    #endregion

    #region RepairBrokenStreamAsync (from BrokenInfo) Tests

    [Fact]
    public async Task RepairBrokenStreamAsync_should_remove_orphaned_events_and_clear_broken_state()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);

        var brokenInfo = new BrokenStreamInfo
        {
            BrokenAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            OrphanedFromVersion = 2,
            OrphanedToVersion = 4,
            ErrorMessage = "Cleanup failed"
        };
        var document = CreateDoc(isBroken: true, brokenInfo: brokenInfo);

        // Add some events (0, 1 are valid, 2-4 are orphaned)
        await dataStore.AppendAsync(document,
            new TestEvent { EventVersion = 0 },
            new TestEvent { EventVersion = 1 },
            new TestEvent { EventVersion = 2 },
            new TestEvent { EventVersion = 3 },
            new TestEvent { EventVersion = 4 });

        // Act
        var result = await service.RepairBrokenStreamAsync(document);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.EventsRemoved);
        Assert.NotNull(result.RollbackRecord);
        Assert.Equal(2, result.RollbackRecord!.FromVersion);
        Assert.Equal(4, result.RollbackRecord.ToVersion);

        // Verify stream state
        Assert.False(document.Active.IsBroken);
        Assert.Null(document.Active.BrokenInfo);
        Assert.NotNull(document.Active.RollbackHistory);
        Assert.Single(document.Active.RollbackHistory);

        // Verify events
        var remainingEvents = await dataStore.ReadAsync(document);
        Assert.Equal(2, remainingEvents!.Count());
    }

    [Fact]
    public async Task RepairBrokenStreamAsync_should_throw_when_stream_not_broken()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc(isBroken: false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RepairBrokenStreamAsync(document));
    }

    [Fact]
    public async Task RepairBrokenStreamAsync_should_throw_when_brokenInfo_is_null()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc(isBroken: true, brokenInfo: null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RepairBrokenStreamAsync(document));
    }

    [Fact]
    public async Task RepairBrokenStreamAsync_should_throw_on_null_document()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.RepairBrokenStreamAsync(null!));
    }

    #endregion

    #region RepairBrokenStreamAsync (with explicit range) Tests

    [Fact]
    public async Task RepairBrokenStreamAsync_with_range_should_remove_specified_versions()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc();

        await dataStore.AppendAsync(document,
            new TestEvent { EventVersion = 0 },
            new TestEvent { EventVersion = 1 },
            new TestEvent { EventVersion = 2 },
            new TestEvent { EventVersion = 3 });

        // Act
        var result = await service.RepairBrokenStreamAsync(document, 1, 2, "Manual cleanup");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.EventsRemoved);
        Assert.NotNull(result.RollbackRecord);

        var remaining = await dataStore.ReadAsync(document);
        Assert.Equal(2, remaining!.Count());
        Assert.Equal(0, remaining!.First().EventVersion);
        Assert.Equal(3, remaining!.Last().EventVersion);
    }

    [Fact]
    public async Task RepairBrokenStreamAsync_with_range_should_clear_broken_state_if_present()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);

        var brokenInfo = new BrokenStreamInfo
        {
            OrphanedFromVersion = 1,
            OrphanedToVersion = 2
        };
        var document = CreateDoc(isBroken: true, brokenInfo: brokenInfo);

        await dataStore.AppendAsync(document,
            new TestEvent { EventVersion = 0 },
            new TestEvent { EventVersion = 1 },
            new TestEvent { EventVersion = 2 });

        // Act
        var result = await service.RepairBrokenStreamAsync(document, 1, 2);

        // Assert
        Assert.True(result.Success);
        Assert.False(document.Active.IsBroken);
        Assert.Null(document.Active.BrokenInfo);
    }

    [Fact]
    public async Task RepairBrokenStreamAsync_with_range_should_add_to_rollback_history()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc();

        // Add existing rollback history
        document.Active.RollbackHistory = [
            new RollbackRecord { FromVersion = 5, ToVersion = 7, EventsRemoved = 3 }
        ];

        await dataStore.AppendAsync(document,
            new TestEvent { EventVersion = 0 },
            new TestEvent { EventVersion = 1 });

        // Act
        var result = await service.RepairBrokenStreamAsync(document, 1, 1);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, document.Active.RollbackHistory.Count);
        Assert.Equal(5, document.Active.RollbackHistory[0].FromVersion);
        Assert.Equal(1, document.Active.RollbackHistory[1].FromVersion);
    }

    [Fact]
    public async Task RepairBrokenStreamAsync_should_return_zero_when_no_events_to_remove()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc();

        // Act
        var result = await service.RepairBrokenStreamAsync(document, 10, 20);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.EventsRemoved);
    }

    #endregion

    #region AppendRollbackMarkerAsync Tests

    [Fact]
    public async Task AppendRollbackMarkerAsync_should_append_marker_event()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc();
        document.Active.CurrentStreamVersion = 5;

        var rollbackRecord = new RollbackRecord
        {
            RolledBackAt = DateTimeOffset.UtcNow,
            FromVersion = 3,
            ToVersion = 5,
            EventsRemoved = 3,
            OriginalError = "Test error"
        };

        // Act
        await service.AppendRollbackMarkerAsync(document, rollbackRecord, "test-correlation-id");

        // Assert
        Assert.Equal(6, document.Active.CurrentStreamVersion);
        var events = await dataStore.ReadAsync(document);
        Assert.NotNull(events);
        Assert.Single(events!);
        Assert.Equal(EventsRolledBackEvent.EventTypeName, events!.First().EventType);
    }

    [Fact]
    public async Task AppendRollbackMarkerAsync_should_throw_on_null_document()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var rollbackRecord = new RollbackRecord();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.AppendRollbackMarkerAsync(null!, rollbackRecord));
    }

    [Fact]
    public async Task AppendRollbackMarkerAsync_should_throw_on_null_rollbackRecord()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.AppendRollbackMarkerAsync(document, null!));
    }

    #endregion

    #region FindBrokenStreamsAsync Tests

    [Fact]
    public async Task FindBrokenStreamsAsync_should_throw_not_supported()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.FindBrokenStreamsAsync());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task RepairBrokenStreamAsync_should_handle_multiple_repairs_on_same_stream()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc();

        // Add 10 events
        for (int i = 0; i < 10; i++)
        {
            await dataStore.AppendAsync(document, new TestEvent { EventVersion = i });
        }

        // Act - perform multiple repairs
        var result1 = await service.RepairBrokenStreamAsync(document, 8, 9);
        var result2 = await service.RepairBrokenStreamAsync(document, 5, 6);
        var result3 = await service.RepairBrokenStreamAsync(document, 2, 3);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.True(result3.Success);

        Assert.Equal(3, document.Active.RollbackHistory!.Count);

        var remaining = await dataStore.ReadAsync(document);
        Assert.Equal(4, remaining!.Count()); // 0, 1, 4, 7
    }

    [Fact]
    public async Task RepairBrokenStreamAsync_should_preserve_rollback_record_timestamps()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);
        var document = CreateDoc();

        await dataStore.AppendAsync(document, new TestEvent { EventVersion = 0 });

        var beforeRepair = DateTimeOffset.UtcNow;

        // Act
        await Task.Delay(10); // Small delay to ensure timestamp difference
        var result = await service.RepairBrokenStreamAsync(document, 0, 0);

        // Assert
        Assert.NotNull(result.RollbackRecord);
        Assert.True(result.RollbackRecord!.RolledBackAt >= beforeRepair);
    }

    [Fact]
    public async Task RepairBrokenStreamAsync_should_include_original_error_in_record()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var documentStore = new InMemoryDocumentStore();
        var service = CreateService(dataStore, documentStore);

        var brokenInfo = new BrokenStreamInfo
        {
            BrokenAt = DateTimeOffset.UtcNow,
            OrphanedFromVersion = 0,
            OrphanedToVersion = 0,
            ErrorMessage = "Original timeout error"
        };
        var document = CreateDoc(isBroken: true, brokenInfo: brokenInfo);

        await dataStore.AppendAsync(document, new TestEvent { EventVersion = 0 });

        // Act
        var result = await service.RepairBrokenStreamAsync(document);

        // Assert
        Assert.NotNull(result.RollbackRecord);
        Assert.Equal("Original timeout error", result.RollbackRecord!.OriginalError);
    }

    #endregion
}
