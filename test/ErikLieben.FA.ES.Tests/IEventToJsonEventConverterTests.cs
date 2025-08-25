using System.Text.Json;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests;

public class IEventToJsonEventConverterTests
{
    [Fact]
    public void Should_deserialize_json_to_event()
    {
        // Arrange
        var sut = new IEventToJsonEventConverter();
        var json =
            @"{""type"":""test.event"",""version"":1,""exseq"":""123"",""action"":{""CorrelationId"":""corr123""}}";
        var jsonElement = JsonDocument.Parse(json).RootElement;
        var reader = new Utf8JsonReader(JsonSerializer.SerializeToUtf8Bytes(jsonElement));
        reader.Read(); // Move to first token

        // Act
        var result = sut.Read(ref reader, typeof(IEvent), new JsonSerializerOptions());

        // Assert
        Assert.NotNull(result);
        Assert.IsType<JsonEvent>(result);
        var jsonEvent = (JsonEvent)result;
        Assert.Equal("test.event", jsonEvent.EventType);
        Assert.Equal(1, jsonEvent.EventVersion);
        Assert.Equal("123", jsonEvent.ExternalSequencer);
        Assert.Equal("corr123", jsonEvent.ActionMetadata.CorrelationId);
    }

    [Fact]
    public void Should_serialize_event_to_json()
    {
        // Arrange
        var sut = new IEventToJsonEventConverter();
        var @event = new JsonEvent
        {
            EventType = "test.event",
            EventVersion = 1,
            ExternalSequencer = "123",
            ActionMetadata = new ActionMetadata("corr123")
        };

        using var memoryStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(memoryStream);

        // Act
        sut.Write(writer, @event, new JsonSerializerOptions());
        writer.Flush();

        // Assert
        memoryStream.Position = 0;
        using var document = JsonDocument.Parse(memoryStream.ToArray());
        var root = document.RootElement;

        Assert.Equal("test.event", root.GetProperty("type").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("123", root.GetProperty("exseq").GetString());
        Assert.Equal("corr123", root.GetProperty("action").GetProperty("CorrelationId").GetString());
    }

    [Fact]
    public void Should_handle_null_when_deserializing()
    {
        // Arrange
        var sut = new IEventToJsonEventConverter();
        var json = "null";
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        reader.Read(); // Move to first token

        // Act
        var result = sut.Read(ref reader, typeof(IEvent), new JsonSerializerOptions());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Should_serialize_event_with_metadata()
    {
        // Arrange
        var sut = new IEventToJsonEventConverter();
        var metadata = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        var @event = new JsonEvent
        {
            EventType = "test.event",
            EventVersion = 1,
            Metadata = metadata
        };

        using var memoryStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(memoryStream);

        // Act
        sut.Write(writer, @event, new JsonSerializerOptions());
        writer.Flush();

        // Assert
        memoryStream.Position = 0;
        using var document = JsonDocument.Parse(memoryStream.ToArray());
        var root = document.RootElement;

        Assert.Equal("test.event", root.GetProperty("type").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.True(root.TryGetProperty("Metadata", out var metadataElement));
        Assert.Equal("value1", metadataElement.GetProperty("key1").GetString());
        Assert.Equal("value2", metadataElement.GetProperty("key2").GetString());
    }

    [Fact]
    public void Should_deserialize_event_with_metadata()
    {
        // Arrange
        var sut = new IEventToJsonEventConverter();
        var json = @"{""type"":""test.event"",""version"":1,""Metadata"":{""key1"":""value1"",""key2"":""value2""}}";
        var jsonElement = JsonDocument.Parse(json).RootElement;
        var reader = new Utf8JsonReader(JsonSerializer.SerializeToUtf8Bytes(jsonElement));
        reader.Read(); // Move to first token

        // Act
        var result = sut.Read(ref reader, typeof(IEvent), new JsonSerializerOptions());

        // Assert
        Assert.NotNull(result);
        Assert.IsType<JsonEvent>(result);
        var jsonEvent = (JsonEvent)result;
        Assert.Equal("test.event", jsonEvent.EventType);
        Assert.Equal(1, jsonEvent.EventVersion);
        Assert.NotNull(jsonEvent.Metadata);
        Assert.Equal(2, jsonEvent.Metadata.Count);
        Assert.Equal("value1", jsonEvent.Metadata["key1"]);
        Assert.Equal("value2", jsonEvent.Metadata["key2"]);
    }

    // [Fact]
    // public void Should_deserialize_event_with_payload()
    // {
    //     // Arrange
    //     var sut = new IEventToJsonEventConverter();
    //     var json =
    //         @"{""type"":""test.event"",""version"":1,""Payload"":""{\""id\"":\""123\"",\""name\"":\""Test\""}""}}";
    //     var jsonElement = JsonDocument.Parse(json).RootElement;
    //     var reader = new Utf8JsonReader(JsonSerializer.SerializeToUtf8Bytes(jsonElement));
    //     reader.Read(); // Move to first token
    //
    //     // Act
    //     var result = sut.Read(ref reader, typeof(IEvent), new JsonSerializerOptions());
    //
    //     // Assert
    //     Assert.NotNull(result);
    //     Assert.IsType<JsonEvent>(result);
    //     var jsonEvent = (JsonEvent)result;
    //     Assert.Equal("test.event", jsonEvent.EventType);
    //     Assert.Equal(1, jsonEvent.EventVersion);
    //     Assert.NotNull(jsonEvent.Payload);
    //     Assert.Contains("id", jsonEvent.Payload);
    //     Assert.Contains("name", jsonEvent.Payload);
    // }

    [Fact]
    public void Should_serialize_event_with_action_metadata()
    {
        // Arrange
        var sut = new IEventToJsonEventConverter();
        var dateTime = DateTimeOffset.UtcNow;
        var actionMetadata = new ActionMetadata
        {
            CorrelationId = "corr123",
            CausationId = "cause456",
            EventOccuredAt = dateTime
        };

        var @event = new JsonEvent
        {
            EventType = "test.event",
            EventVersion = 1,
            ActionMetadata = actionMetadata
        };

        using var memoryStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(memoryStream);

        // Act
        sut.Write(writer, @event, new JsonSerializerOptions());
        writer.Flush();

        // Assert
        memoryStream.Position = 0;
        using var document = JsonDocument.Parse(memoryStream.ToArray());
        var root = document.RootElement;

        var actionElement = root.GetProperty("action");
        Assert.Equal("corr123", actionElement.GetProperty("CorrelationId").GetString());
        Assert.Equal("cause456", actionElement.GetProperty("CausationId").GetString());

        // Convert both to the same format for comparison to avoid precision issues
        var expectedDateTimeString = JsonSerializer.Serialize(dateTime).Trim('"');
        var actualDateTimeString = actionElement.GetProperty("EventOccuredAt").GetRawText().Trim('"');
        Assert.Equal(expectedDateTimeString, actualDateTimeString);
    }
}
