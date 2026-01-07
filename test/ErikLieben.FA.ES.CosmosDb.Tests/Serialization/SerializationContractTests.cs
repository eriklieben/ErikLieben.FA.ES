using System.Text.Json;
using ErikLieben.FA.ES.CosmosDb.Model;
using ErikLieben.FA.ES.CosmosDb.Serialization;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Serialization;

/// <summary>
/// Tests that verify the serialization contract between entity classes and queries.
/// These tests ensure that the JSON property names match what the queries expect.
/// </summary>
/// <remarks>
/// These tests exist because a previous bug occurred where queries used `c._type`
/// but the actual serialized property name was `type` due to serializer configuration
/// differences between production and test code. This test suite prevents similar
/// regressions.
/// </remarks>
public class SerializationContractTests
{
    private readonly JsonSerializerOptions _options;

    public SerializationContractTests()
    {
        _options = CosmosDbSystemTextJsonSerializer.CreateDefaultOptions();
    }

    [Fact]
    public void EventEntity_Type_SerializesTo_Underscore_Type()
    {
        // Arrange
        var entity = new CosmosDbEventEntity
        {
            Id = "test-id",
            StreamId = "test-stream",
            Version = 0,
            EventType = "TestEvent",
            Data = "{}",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(entity, _options);
        using var doc = JsonDocument.Parse(json);

        // Assert - The type discriminator MUST be "_type" to match queries
        Assert.True(doc.RootElement.TryGetProperty("_type", out var typeProperty),
            "Expected '_type' property in serialized CosmosDbEventEntity. " +
            "This property is used in queries like 'WHERE c._type = \"event\"'. " +
            "If this test fails, queries will not return any results.");
        Assert.Equal("event", typeProperty.GetString());
    }

    [Fact]
    public void EventEntity_Type_DoesNotSerializeTo_Plain_Type()
    {
        // Arrange
        var entity = new CosmosDbEventEntity
        {
            Id = "test-id",
            StreamId = "test-stream",
            Version = 0,
            EventType = "TestEvent",
            Data = "{}",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(entity, _options);
        using var doc = JsonDocument.Parse(json);

        // Assert - Should NOT have a plain "type" property (that was the bug)
        Assert.False(doc.RootElement.TryGetProperty("type", out _),
            "Found unexpected 'type' property. The property should be '_type'. " +
            "This indicates the [JsonPropertyName(\"_type\")] attribute is not being respected.");
    }

    [Fact]
    public void DocumentEntity_Type_SerializesTo_Underscore_Type()
    {
        // Arrange
        var entity = new CosmosDbDocumentEntity
        {
            Id = "test-id",
            ObjectName = "test-object",
            ObjectId = "obj-123",
            Active = new CosmosDbStreamInfo
            {
                StreamIdentifier = "test-stream",
                CurrentStreamVersion = 0
            }
        };

        // Act
        var json = JsonSerializer.Serialize(entity, _options);
        using var doc = JsonDocument.Parse(json);

        // Assert
        Assert.True(doc.RootElement.TryGetProperty("_type", out var typeProperty),
            "Expected '_type' property in serialized CosmosDbDocumentEntity.");
        Assert.Equal("document", typeProperty.GetString());
    }

    [Fact]
    public void SnapshotEntity_Type_SerializesTo_Underscore_Type()
    {
        // Arrange
        var entity = new CosmosDbSnapshotEntity
        {
            Id = "test-id",
            StreamId = "test-stream",
            Version = 0,
            Name = "TestSnapshot",
            Data = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(entity, _options);
        using var doc = JsonDocument.Parse(json);

        // Assert
        Assert.True(doc.RootElement.TryGetProperty("_type", out var typeProperty),
            "Expected '_type' property in serialized CosmosDbSnapshotEntity.");
        Assert.Equal("snapshot", typeProperty.GetString());
    }

    [Fact]
    public void TagEntity_Type_SerializesTo_Underscore_Type()
    {
        // Arrange
        var entity = new CosmosDbTagEntity
        {
            Id = "test-id",
            TagKey = "test-key",
            Tag = "test-value",
            ObjectName = "test-object",
            ObjectId = "obj-123"
        };

        // Act
        var json = JsonSerializer.Serialize(entity, _options);
        using var doc = JsonDocument.Parse(json);

        // Assert
        Assert.True(doc.RootElement.TryGetProperty("_type", out var typeProperty),
            "Expected '_type' property in serialized CosmosDbTagEntity.");
        Assert.Equal("tag", typeProperty.GetString());
    }

    [Fact]
    public void EventEntity_CamelCaseProperties_AreRespected()
    {
        // Arrange
        var entity = new CosmosDbEventEntity
        {
            Id = "test-id",
            StreamId = "test-stream",
            Version = 5,
            EventType = "TestEvent",
            Data = "{\"key\":\"value\"}",
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(entity, _options);
        using var doc = JsonDocument.Parse(json);

        // Assert - Verify camelCase naming for regular properties
        Assert.True(doc.RootElement.TryGetProperty("streamId", out _),
            "Expected 'streamId' property (camelCase)");
        Assert.True(doc.RootElement.TryGetProperty("version", out _),
            "Expected 'version' property (camelCase)");
        Assert.True(doc.RootElement.TryGetProperty("eventType", out _),
            "Expected 'eventType' property (camelCase)");
        Assert.True(doc.RootElement.TryGetProperty("data", out _),
            "Expected 'data' property (camelCase)");
    }

    [Fact]
    public void ProjectionDocument_SerializesCorrectly()
    {
        // Arrange
        var document = new ProjectionDocument
        {
            Id = "test-projection",
            ProjectionName = "TestProjection",
            Data = "{\"items\":[]}",
            LastModified = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(document, _options);
        using var doc = JsonDocument.Parse(json);

        // Assert
        Assert.True(doc.RootElement.TryGetProperty("id", out _), "Expected 'id' property");
        Assert.True(doc.RootElement.TryGetProperty("projectionName", out _), "Expected 'projectionName' property");
        Assert.True(doc.RootElement.TryGetProperty("data", out _), "Expected 'data' property");
        Assert.True(doc.RootElement.TryGetProperty("lastModified", out _), "Expected 'lastModified' property");
    }

    [Fact]
    public void CheckpointDocument_SerializesCorrectly()
    {
        // Arrange
        var document = new CheckpointDocument
        {
            Id = "checkpoint-123",
            ProjectionName = "TestProjection",
            Fingerprint = "abc123",
            Data = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(document, _options);
        using var doc = JsonDocument.Parse(json);

        // Assert
        Assert.True(doc.RootElement.TryGetProperty("id", out _), "Expected 'id' property");
        Assert.True(doc.RootElement.TryGetProperty("projectionName", out _), "Expected 'projectionName' property");
        Assert.True(doc.RootElement.TryGetProperty("fingerprint", out _), "Expected 'fingerprint' property");
        Assert.True(doc.RootElement.TryGetProperty("data", out _), "Expected 'data' property");
        Assert.True(doc.RootElement.TryGetProperty("createdAt", out _), "Expected 'createdAt' property");
    }

    [Fact]
    public void RoundTrip_EventEntity_PreservesAllProperties()
    {
        // Arrange
        var original = new CosmosDbEventEntity
        {
            Id = "test-id-123",
            StreamId = "stream-456",
            Version = 42,
            EventType = "SomethingHappened",
            Data = "{\"foo\":\"bar\",\"count\":123}",
            Timestamp = DateTimeOffset.Parse("2024-01-15T10:30:00Z"),
            Ttl = 3600
        };

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<CosmosDbEventEntity>(json, _options);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.StreamId, deserialized.StreamId);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.Equal(original.EventType, deserialized.EventType);
        Assert.Equal(original.Data, deserialized.Data);
        Assert.Equal(original.Type, deserialized.Type); // Should be "event"
        Assert.Equal(original.Ttl, deserialized.Ttl);
    }

    [Fact]
    public void Serializer_SerializeMemberName_Respects_JsonPropertyName()
    {
        // Arrange
        var serializer = new CosmosDbSystemTextJsonSerializer();
        var typeProperty = typeof(CosmosDbEventEntity).GetProperty(nameof(CosmosDbEventEntity.Type))!;

        // Act
        var memberName = serializer.SerializeMemberName(typeProperty);

        // Assert - This is critical for LINQ query translation
        Assert.Equal("_type", memberName);
    }

    [Fact]
    public void Serializer_SerializeMemberName_AppliesCamelCase_WhenNoAttribute()
    {
        // Arrange
        var serializer = new CosmosDbSystemTextJsonSerializer();
        var streamIdProperty = typeof(CosmosDbEventEntity).GetProperty(nameof(CosmosDbEventEntity.StreamId))!;

        // Act
        var memberName = serializer.SerializeMemberName(streamIdProperty);

        // Assert
        Assert.Equal("streamId", memberName);
    }
}
