using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Table.Model;

/// <summary>
/// Represents a JSON-serializable event stored in Table Storage with a strongly-typed JSON payload.
/// </summary>
public record TableJsonEvent : JsonEvent
{
    /// <summary>
    /// Gets or sets the event payload as raw JSON. Defaults to an empty JSON object when underlying payload is null.
    /// </summary>
    [JsonPropertyName("payload")]
    [JsonConverter(typeof(TablePayloadConverter))]
    public new string Payload
    {
        get => base.Payload ?? "{}";
        set => base.Payload = value;
    }

    /// <summary>
    /// Creates a <see cref="TableJsonEvent"/> from a generic <see cref="IEvent"/>, copying metadata.
    /// </summary>
    /// <param name="event">The source event instance.</param>
    /// <param name="preserveTimestamp">Unused parameter, kept for API compatibility. Azure Table Storage manages timestamps automatically.</param>
    /// <returns>A new <see cref="TableJsonEvent"/> or the same instance when already of this type; null when conversion is not possible.</returns>
    public new static TableJsonEvent? From(IEvent @event, bool preserveTimestamp = false)
    {
        var jsonEvent = @event as JsonEvent;

        if (jsonEvent == null)
        {
            return null;
        }

        if (jsonEvent is TableJsonEvent tableJsonEvent)
        {
            return tableJsonEvent;
        }

        return new TableJsonEvent
        {
            Payload = jsonEvent.Payload ?? "{}",
            ActionMetadata = jsonEvent.ActionMetadata,
            Metadata = jsonEvent.Metadata,
            EventType = jsonEvent.EventType,
            EventVersion = jsonEvent.EventVersion,
            SchemaVersion = jsonEvent.SchemaVersion
        };
    }

    /// <summary>
    /// Creates a <see cref="TableJsonEvent"/> from a <see cref="TableEventEntity"/>.
    /// </summary>
    /// <param name="entity">The table entity to convert.</param>
    /// <returns>A new <see cref="TableJsonEvent"/> instance.</returns>
    public static TableJsonEvent FromEntity(TableEventEntity entity)
    {
        ActionMetadata? actionMetadata = null;
        if (!string.IsNullOrEmpty(entity.ActionMetadata))
        {
            actionMetadata = JsonSerializer.Deserialize(entity.ActionMetadata, ActionMetadataContext.Default.ActionMetadata);
        }

        Dictionary<string, string>? metadata = null;
        if (!string.IsNullOrEmpty(entity.Metadata))
        {
            metadata = JsonSerializer.Deserialize(entity.Metadata, MetadataDictionaryContext.Default.DictionaryStringString);
        }

        return new TableJsonEvent
        {
            Payload = entity.Payload,
            ActionMetadata = actionMetadata ?? new ActionMetadata(),
            Metadata = metadata ?? [],
            EventType = entity.EventType,
            EventVersion = entity.EventVersion,
            SchemaVersion = entity.SchemaVersion
        };
    }

    /// <summary>
    /// Converts this event to a <see cref="TableEventEntity"/> for storage.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <param name="streamIdentifier">The stream identifier.</param>
    /// <param name="lastObjectDocumentHash">The last object document hash.</param>
    /// <param name="chunkIdentifier">Optional chunk identifier.</param>
    /// <returns>A new <see cref="TableEventEntity"/> instance.</returns>
    public TableEventEntity ToEntity(
        string objectId,
        string streamIdentifier,
        string lastObjectDocumentHash,
        int? chunkIdentifier = null)
    {
        // Table name already indicates the object type, so partition key only needs stream identifier
        var partitionKey = chunkIdentifier.HasValue
            ? $"{streamIdentifier}_{chunkIdentifier:d10}"
            : streamIdentifier;

        string? actionMetadataJson = null;
        if (ActionMetadata != null)
        {
            actionMetadataJson = JsonSerializer.Serialize(ActionMetadata, ActionMetadataContext.Default.ActionMetadata);
        }

        string? metadataJson = null;
        if (Metadata != null && Metadata.Count > 0)
        {
            metadataJson = JsonSerializer.Serialize(Metadata, MetadataDictionaryContext.Default.DictionaryStringString);
        }

        return new TableEventEntity
        {
            PartitionKey = partitionKey,
            RowKey = $"{EventVersion:d20}",
            ObjectId = objectId,
            StreamIdentifier = streamIdentifier,
            EventVersion = EventVersion,
            EventType = EventType,
            SchemaVersion = SchemaVersion,
            Payload = Payload,
            ActionMetadata = actionMetadataJson,
            Metadata = metadataJson,
            ChunkIdentifier = chunkIdentifier,
            LastObjectDocumentHash = lastObjectDocumentHash
        };
    }
}

/// <summary>
/// JSON converter that reads/writes payload values as raw JSON strings rather than quoted strings.
/// </summary>
public class TablePayloadConverter : JsonConverter<string>
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
[JsonSerializable(typeof(TableJsonEvent))]
internal partial class TableJsonEventContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(ActionMetadata))]
internal partial class ActionMetadataContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class MetadataDictionaryContext : JsonSerializerContext
{
}
