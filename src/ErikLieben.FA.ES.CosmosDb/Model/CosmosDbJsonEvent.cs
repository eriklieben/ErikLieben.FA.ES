using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CosmosDb.Model;

/// <summary>
/// Represents a JSON-serializable event stored in CosmosDB with a strongly-typed JSON payload.
/// </summary>
public record CosmosDbJsonEvent : JsonEvent
{
    /// <summary>
    /// Gets or sets the original timestamp when the event was created (for migrations).
    /// </summary>
    public DateTimeOffset? OriginalTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the event payload as raw JSON. Defaults to an empty JSON object when underlying payload is null.
    /// </summary>
    [JsonPropertyName("payload")]
    [JsonConverter(typeof(CosmosDbPayloadConverter))]
    public new string Payload
    {
        get => base.Payload ?? "{}";
        set => base.Payload = value;
    }

    /// <summary>
    /// Creates a <see cref="CosmosDbJsonEvent"/> from a generic <see cref="IEvent"/>, copying metadata.
    /// </summary>
    /// <param name="event">The source event instance.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp for migrations.</param>
    /// <returns>A new <see cref="CosmosDbJsonEvent"/> or the same instance when already of this type; null when conversion is not possible.</returns>
    public static CosmosDbJsonEvent? From(IEvent @event, bool preserveTimestamp = false)
    {
        var jsonEvent = @event as JsonEvent;

        if (jsonEvent == null)
        {
            return null;
        }

        if (jsonEvent is CosmosDbJsonEvent cosmosDbJsonEvent)
        {
            return cosmosDbJsonEvent;
        }

        var result = new CosmosDbJsonEvent
        {
            Payload = jsonEvent.Payload ?? "{}",
            ActionMetadata = jsonEvent.ActionMetadata,
            Metadata = jsonEvent.Metadata,
            EventType = jsonEvent.EventType,
            EventVersion = jsonEvent.EventVersion,
            SchemaVersion = jsonEvent.SchemaVersion,
            ExternalSequencer = jsonEvent.ExternalSequencer
        };

        // Preserve timestamp for migrations if available from source
        if (preserveTimestamp && jsonEvent is CosmosDbJsonEvent source && source.OriginalTimestamp.HasValue)
        {
            result.OriginalTimestamp = source.OriginalTimestamp;
        }

        return result;
    }

    /// <summary>
    /// Creates a <see cref="CosmosDbJsonEvent"/> from a <see cref="CosmosDbEventEntity"/>.
    /// </summary>
    /// <param name="entity">The CosmosDB entity to convert.</param>
    /// <returns>A new <see cref="CosmosDbJsonEvent"/> instance.</returns>
    public static CosmosDbJsonEvent FromEntity(CosmosDbEventEntity entity)
    {
        ActionMetadata? actionMetadata = null;
        Dictionary<string, string>? metadata = null;

        // Parse action metadata if present in the Data field
        if (!string.IsNullOrEmpty(entity.Data))
        {
            try
            {
                using var doc = JsonDocument.Parse(entity.Data);
                var root = doc.RootElement;

                if (root.TryGetProperty("action", out var actionElement))
                {
                    actionMetadata = JsonSerializer.Deserialize(actionElement.GetRawText(), CosmosDbActionMetadataContext.Default.ActionMetadata);
                }

                if (root.TryGetProperty("metadata", out var metadataElement))
                {
                    metadata = JsonSerializer.Deserialize(metadataElement.GetRawText(), CosmosDbMetadataDictionaryContext.Default.DictionaryStringString);
                }
            }
            catch
            {
                // If parsing fails, use defaults
            }
        }

        return new CosmosDbJsonEvent
        {
            Payload = entity.Data,
            ActionMetadata = actionMetadata ?? new ActionMetadata(),
            Metadata = metadata ?? [],
            EventType = entity.EventType,
            EventVersion = entity.Version,
            SchemaVersion = entity.SchemaVersion,
            ExternalSequencer = entity.ExternalSequencer,
            OriginalTimestamp = entity.Timestamp
        };
    }

    /// <summary>
    /// Converts this event to a <see cref="CosmosDbEventEntity"/> for storage.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp.</param>
    /// <returns>A new <see cref="CosmosDbEventEntity"/> instance.</returns>
    public CosmosDbEventEntity ToEntity(string streamId, bool preserveTimestamp = false)
    {
        var timestamp = preserveTimestamp && OriginalTimestamp.HasValue
            ? OriginalTimestamp.Value
            : DateTimeOffset.UtcNow;

        return new CosmosDbEventEntity
        {
            Id = CosmosDbEventEntity.CreateId(streamId, EventVersion),
            StreamId = streamId,
            Version = EventVersion,
            EventType = EventType,
            SchemaVersion = SchemaVersion,
            Data = Payload,
            Timestamp = timestamp,
            ExternalSequencer = ExternalSequencer,
            CorrelationId = ActionMetadata?.CorrelationId,
            CausationId = ActionMetadata?.CausationId
        };
    }
}

/// <summary>
/// JSON converter that reads/writes payload values as raw JSON strings rather than quoted strings.
/// </summary>
public class CosmosDbPayloadConverter : JsonConverter<string>
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

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CosmosDbJsonEvent))]
internal partial class CosmosDbJsonEventContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(ActionMetadata))]
internal partial class CosmosDbActionMetadataContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class CosmosDbMetadataDictionaryContext : JsonSerializerContext
{
}
