using System.Text.Json.Serialization;
using ErikLieben.FA.ES;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

[JsonSerializable(typeof(TestEntity))]
[JsonSerializable(typeof(JsonEvent))]
internal partial class TestJsonContext : JsonSerializerContext
{
}
