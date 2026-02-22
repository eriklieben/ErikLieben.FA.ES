using System.Text.Json.Serialization;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// AOT-compatible JSON serializer context for Checkpoint serialization.
/// </summary>
[JsonSerializable(typeof(Checkpoint))]
[JsonSerializable(typeof(ObjectIdentifier))]
[JsonSerializable(typeof(VersionIdentifier))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class CheckpointJsonContext : JsonSerializerContext
{
}
