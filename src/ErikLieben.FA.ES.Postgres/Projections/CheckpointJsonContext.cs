using System.Text.Json.Serialization;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.Postgres.Projections;

/// <summary>
/// AOT-compatible JSON serializer context for Checkpoint serialization in the Postgres provider.
/// </summary>
[JsonSerializable(typeof(Checkpoint))]
[JsonSerializable(typeof(ObjectIdentifier))]
[JsonSerializable(typeof(VersionIdentifier))]
internal partial class CheckpointJsonContext : JsonSerializerContext
{
}
