using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES;

[JsonSerializable(typeof(JsonEvent))]
internal partial class JsonEventSerializerContext : JsonSerializerContext 
{ 
}