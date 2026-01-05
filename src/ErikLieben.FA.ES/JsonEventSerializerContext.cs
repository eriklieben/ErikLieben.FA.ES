using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES;

/// <summary>
/// JSON serializer context for AOT-compatible event serialization.
/// </summary>
[JsonSerializable(typeof(JsonEvent))]
public partial class JsonEventSerializerContext : JsonSerializerContext
{
}