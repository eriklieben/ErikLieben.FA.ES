using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES;

public class IEventToJsonEventConverter : JsonConverter<IEvent>
{
    public override IEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<JsonEvent>(ref reader, JsonEventSerializerContext.Default.JsonEvent);
    }

    public override void Write(Utf8JsonWriter writer, IEvent value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, JsonEventSerializerContext.Default.JsonEvent);
    }
}