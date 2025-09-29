using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES;

/// <summary>
/// JSON converter that serializes and deserializes <see cref="IEvent"/> using the generated <see cref="JsonEventSerializerContext"/>.
/// </summary>
public class IEventToJsonEventConverter : JsonConverter<IEvent>
{
    /// <summary>
    /// Reads JSON and materializes an <see cref="IEvent"/> instance using the source-generated context.
    /// </summary>
    /// <param name="reader">The JSON reader positioned at the start of the event.</param>
    /// <param name="typeToConvert">The runtime type to convert; ignored as <see cref="IEvent"/> is used.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>The deserialized event instance, or null when the JSON token is null.</returns>
    public override IEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<JsonEvent>(ref reader, JsonEventSerializerContext.Default.JsonEvent);
    }

    /// <summary>
    /// Writes the specified <see cref="IEvent"/> to JSON using the source-generated context.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The event to serialize.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(Utf8JsonWriter writer, IEvent value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, JsonEventSerializerContext.Default.JsonEvent);
    }
}
