using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CosmosDb.Model;

/// <summary>
/// JSON serialization context for AOT-friendly serialization of CosmosDB entities.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CosmosDbEventEntity))]
[JsonSerializable(typeof(CosmosDbDocumentEntity))]
[JsonSerializable(typeof(CosmosDbSnapshotEntity))]
[JsonSerializable(typeof(CosmosDbTagEntity))]
[JsonSerializable(typeof(CosmosDbStreamInfo))]
[JsonSerializable(typeof(CosmosDbTerminatedStreamInfo))]
[JsonSerializable(typeof(List<CosmosDbEventEntity>))]
[JsonSerializable(typeof(List<CosmosDbTerminatedStreamInfo>))]
public partial class CosmosDbJsonContext : JsonSerializerContext
{
}
