using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryStreamMetadataProviderTests
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
    }

    private static InMemoryEventStreamDocument CreateDoc(string name = "order", string id = "42")
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
    public void Constructor_should_throw_on_null_dataStore()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new InMemoryStreamMetadataProvider(null!));
    }

    [Fact]
    public async Task GetStreamMetadataAsync_should_return_null_when_stream_does_not_exist()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var provider = new InMemoryStreamMetadataProvider(dataStore);

        // Act
        var result = await provider.GetStreamMetadataAsync("order", "nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStreamMetadataAsync_should_return_null_when_stream_is_empty()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var provider = new InMemoryStreamMetadataProvider(dataStore);
        var key = InMemoryDataStore.GetStoreKey("order", "42");
        dataStore.Store[key] = new Dictionary<int, IEvent>();

        // Act
        var result = await provider.GetStreamMetadataAsync("order", "42");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStreamMetadataAsync_should_return_metadata_with_correct_event_count()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var provider = new InMemoryStreamMetadataProvider(dataStore);
        var doc = CreateDoc("order", "42");
        await dataStore.AppendAsync(doc, default,
            new TestEvent { EventVersion = 0 },
            new TestEvent { EventVersion = 1 },
            new TestEvent { EventVersion = 2 });

        // Act
        var result = await provider.GetStreamMetadataAsync("order", "42");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result!.EventCount);
    }

    [Fact]
    public async Task GetStreamMetadataAsync_should_return_correct_object_name_and_id()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var provider = new InMemoryStreamMetadataProvider(dataStore);
        var doc = CreateDoc("project", "abc-123");
        await dataStore.AppendAsync(doc, default, new TestEvent { EventVersion = 0 });

        // Act
        var result = await provider.GetStreamMetadataAsync("project", "abc-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("project", result!.ObjectName);
        Assert.Equal("abc-123", result.ObjectId);
    }

    [Fact]
    public async Task GetStreamMetadataAsync_should_return_null_dates_for_in_memory_storage()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var provider = new InMemoryStreamMetadataProvider(dataStore);
        var doc = CreateDoc("order", "42");
        await dataStore.AppendAsync(doc, default, new TestEvent { EventVersion = 0 });

        // Act
        var result = await provider.GetStreamMetadataAsync("order", "42");

        // Assert
        Assert.NotNull(result);
        Assert.Null(result!.OldestEventDate);
        Assert.Null(result.NewestEventDate);
    }

    [Fact]
    public async Task GetStreamMetadataAsync_should_differentiate_between_different_streams()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var provider = new InMemoryStreamMetadataProvider(dataStore);

        var doc1 = CreateDoc("order", "1");
        await dataStore.AppendAsync(doc1, default, new TestEvent { EventVersion = 0 });

        var doc2 = CreateDoc("order", "2");
        await dataStore.AppendAsync(doc2, default,
            new TestEvent { EventVersion = 0 },
            new TestEvent { EventVersion = 1 });

        // Act
        var result1 = await provider.GetStreamMetadataAsync("order", "1");
        var result2 = await provider.GetStreamMetadataAsync("order", "2");

        // Assert
        Assert.NotNull(result1);
        Assert.Equal(1, result1!.EventCount);
        Assert.NotNull(result2);
        Assert.Equal(2, result2!.EventCount);
    }

    [Fact]
    public async Task GetStreamMetadataAsync_should_support_cancellation_token()
    {
        // Arrange
        var dataStore = new InMemoryDataStore();
        var provider = new InMemoryStreamMetadataProvider(dataStore);
        var doc = CreateDoc("order", "42");
        await dataStore.AppendAsync(doc, default, new TestEvent { EventVersion = 0 });
        using var cts = new CancellationTokenSource();

        // Act - should not throw when called with a cancellation token
        var result = await provider.GetStreamMetadataAsync("order", "42", cts.Token);

        // Assert
        Assert.NotNull(result);
    }
}
