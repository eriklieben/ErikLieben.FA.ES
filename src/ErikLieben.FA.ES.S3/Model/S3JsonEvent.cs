using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Represents a JSON-serializable event stored in S3 with a strongly-typed JSON payload and a UTC timestamp.
/// </summary>
public record S3JsonEvent : JsonEvent
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
    [JsonConverter(typeof(S3PayloadConverter))]
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
    /// Creates an <see cref="S3JsonEvent"/> from a generic <see cref="IEvent"/>, copying metadata and timestamp.
    /// </summary>
    public static S3JsonEvent? From(IEvent @event, bool preserveTimestamp = false)
    {
        var jsonEvent = @event as JsonEvent;

        if (jsonEvent == null)
        {
            return null;
        }

        if (jsonEvent is S3JsonEvent s3JsonEvent)
        {
            return preserveTimestamp
                ? s3JsonEvent
                : s3JsonEvent with { Timestamp = DateTimeOffset.UtcNow };
        }

        return new S3JsonEvent
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
public class S3PayloadConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.Parse(value);
        doc.WriteTo(writer);
    }
}
