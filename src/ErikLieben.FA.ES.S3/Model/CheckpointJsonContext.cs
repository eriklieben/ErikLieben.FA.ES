using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.S3.Model;

/// <summary>
/// Provides the System.Text.Json source-generation context for <see cref="Checkpoint"/> in S3 storage.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Checkpoint))]
internal partial class CheckpointJsonContext : JsonSerializerContext
{
}
