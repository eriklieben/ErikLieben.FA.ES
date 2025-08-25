using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

public record BlobJsonEvent : JsonEvent
{
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; set; }


    [JsonPropertyName("payload")]
    [JsonConverter(typeof(BlobPayloadConverter))]
    public new string Payload
    {
        get
        {
            return base.Payload ?? "{}";
        } 
        set
        {
            base.Payload = value;
        }
    }

    public new static BlobJsonEvent? From(IEvent @event)
    {
        var jsonEvent = @event as JsonEvent;

        if (jsonEvent == null)
        {
            return null;
        }

        if (jsonEvent is BlobJsonEvent blobJsonEvent)
        {
            return blobJsonEvent with { Timestamp = DateTimeOffset.UtcNow };
        }
        
        return new BlobJsonEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Payload = jsonEvent.Payload ?? "{}",
            ActionMetadata = jsonEvent.ActionMetadata,
            Metadata = jsonEvent.Metadata,
            EventType = jsonEvent.EventType,
            EventVersion = jsonEvent.EventVersion
        };
    }
}

public class BlobPayloadConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.Parse(value);
        doc.WriteTo(writer);
    }
}

