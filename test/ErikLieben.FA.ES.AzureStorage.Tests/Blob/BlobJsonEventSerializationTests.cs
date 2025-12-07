using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

/// <summary>
/// Test serialization context that mirrors the internal BlobDataStoreDocumentContext.
/// Used to verify the serialization behavior of BlobJsonEvent and BlobDataStoreDocument.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BlobDataStoreDocument))]
[JsonSerializable(typeof(BlobJsonEvent))]
internal partial class TestBlobSerializerContext : JsonSerializerContext
{
}

public class BlobJsonEventSerializationTests
{
    [Fact]
    public void Should_serialize_schemaVersion_when_greater_than_1()
    {
        // Arrange
        var blobEvent = new BlobJsonEvent
        {
            EventType = "TestEvent",
            EventVersion = 1,
            SchemaVersion = 2,
            Payload = "{}",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(blobEvent, TestBlobSerializerContext.Default.BlobJsonEvent);

        // Assert
        Assert.Contains("\"schemaVersion\":2", json);
    }

    [Fact]
    public void Should_not_serialize_schemaVersion_when_equals_1()
    {
        // Arrange
        var blobEvent = new BlobJsonEvent
        {
            EventType = "TestEvent",
            EventVersion = 1,
            SchemaVersion = 1,
            Payload = "{}",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(blobEvent, TestBlobSerializerContext.Default.BlobJsonEvent);

        // Assert
        Assert.DoesNotContain("schemaVersion", json);
    }

    [Fact]
    public void Should_deserialize_schemaVersion_correctly()
    {
        // Arrange
        var json = """{"timestamp":"2024-01-01T00:00:00Z","payload":{},"type":"TestEvent","version":1,"schemaVersion":2}""";

        // Act
        var blobEvent = JsonSerializer.Deserialize(json, TestBlobSerializerContext.Default.BlobJsonEvent);

        // Assert
        Assert.NotNull(blobEvent);
        Assert.Equal(2, blobEvent.SchemaVersion);
    }

    [Fact]
    public void Should_default_schemaVersion_to_1_when_not_in_json()
    {
        // Arrange
        var json = """{"timestamp":"2024-01-01T00:00:00Z","payload":{},"type":"TestEvent","version":1}""";

        // Act
        var blobEvent = JsonSerializer.Deserialize(json, TestBlobSerializerContext.Default.BlobJsonEvent);

        // Assert
        Assert.NotNull(blobEvent);
        Assert.Equal(1, blobEvent.SchemaVersion);
    }

    [Fact]
    public void Should_serialize_document_with_schemaVersion_2_events()
    {
        // Arrange
        var document = new BlobDataStoreDocument
        {
            ObjectId = "test-id",
            ObjectName = "TestObject",
            LastObjectDocumentHash = "*"
        };
        document.Events.Add(new BlobJsonEvent
        {
            EventType = "TestEvent",
            EventVersion = 1,
            SchemaVersion = 2,
            Payload = "{}",
            Timestamp = DateTimeOffset.UtcNow
        });

        // Act
        var json = JsonSerializer.Serialize(document, TestBlobSerializerContext.Default.BlobDataStoreDocument);

        // Assert
        Assert.Contains("\"schemaVersion\":2", json);
    }

    [Fact]
    public void BlobJsonEvent_From_should_preserve_schemaVersion()
    {
        // Arrange
        var jsonEvent = new JsonEvent
        {
            EventType = "TestEvent",
            EventVersion = 1,
            SchemaVersion = 2,
            Payload = "{}"
        };

        // Act
        var blobEvent = BlobJsonEvent.From(jsonEvent);

        // Assert
        Assert.NotNull(blobEvent);
        Assert.Equal(2, blobEvent.SchemaVersion);
    }
}
