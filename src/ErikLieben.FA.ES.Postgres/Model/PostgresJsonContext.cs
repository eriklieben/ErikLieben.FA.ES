using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Postgres.Model;

/// <summary>
/// AOT-friendly source-generated JSON context for Postgres provider serialization paths
/// that need to round-trip through STJ (action metadata, metadata dictionary, document JSON).
/// Event payload itself is written/read as raw JSON via Utf8JsonWriter/JsonDocument and does
/// not require type info here.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ActionMetadata))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(PostgresStreamInformation))]
[JsonSerializable(typeof(PostgresTerminatedStream))]
[JsonSerializable(typeof(List<PostgresTerminatedStream>))]
internal partial class PostgresJsonContext : JsonSerializerContext
{
}
