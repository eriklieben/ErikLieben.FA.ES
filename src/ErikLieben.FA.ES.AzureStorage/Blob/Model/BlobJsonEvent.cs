using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Model;

/// <summary>
/// Represents a JSON-serializable event stored in Blob with a strongly-typed JSON payload and a UTC timestamp.
/// </summary>
public record BlobJsonEvent : JsonEvent
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the event was persisted.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; set; }


    /// <summary>
    /// Gets or sets the event payload as raw JSON. Defaults to an empty JSON object when underlying payload is null.
    /// </summary>
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

    /// <summary>
    /// Creates a <see cref="BlobJsonEvent"/> from a generic <see cref="IEvent"/>, copying metadata and timestamp.
    /// </summary>
    /// <param name="event">The source event instance.</param>
    /// <param name="preserveTimestamp">When true and the source is a BlobJsonEvent, preserves the original timestamp. Default is false (uses current UTC time).</param>
    /// <returns>A new <see cref="BlobJsonEvent"/> or the same instance when already of this type; null when conversion is not possible.</returns>
    public static BlobJsonEvent? From(IEvent @event, bool preserveTimestamp = false)
    {
        var jsonEvent = @event as JsonEvent;

        if (jsonEvent == null)
        {
            return null;
        }

        if (jsonEvent is BlobJsonEvent blobJsonEvent)
        {
            // When preserveTimestamp is true, keep the original timestamp (useful for migrations)
            // Otherwise, set a new timestamp (normal append behavior)
            return preserveTimestamp
                ? blobJsonEvent
                : blobJsonEvent with { Timestamp = DateTimeOffset.UtcNow };
        }

        return new BlobJsonEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Payload = jsonEvent.Payload ?? "{}",
            ActionMetadata = jsonEvent.ActionMetadata,
            Metadata = jsonEvent.Metadata,
            EventType = jsonEvent.EventType,
            EventVersion = jsonEvent.EventVersion,
            SchemaVersion = jsonEvent.SchemaVersion
        };
    }
}

/// <summary>
/// JSON converter that reads/writes payload values as raw JSON strings rather than quoted strings.
/// </summary>
public class BlobPayloadConverter : JsonConverter<string>
{
    /// <summary>
    /// Reads a JSON value and returns the raw JSON text.
    /// </summary>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    /// <summary>
    /// Writes a raw JSON string to the writer without additional quoting.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.Parse(value);
        doc.WriteTo(writer);
    }
}
