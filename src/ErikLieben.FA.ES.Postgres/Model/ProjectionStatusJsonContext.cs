using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.Postgres.Model;

/// <summary>
/// AOT-friendly source-generated JSON context for projection-status records persisted
/// to the <c>faes_projection_status</c> jsonb columns.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RebuildInfo))]
[JsonSerializable(typeof(RebuildToken))]
internal partial class ProjectionStatusJsonContext : JsonSerializerContext
{
}
