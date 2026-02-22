namespace ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

using System.Text.Json.Serialization;
using ErikLieben.FA.ES.EventStreamManagement.Events;

/// <summary>
/// JSON serialization context for live migration types.
/// Enables AOT-compatible serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StreamClosedEvent))]
[JsonSerializable(typeof(EventsRolledBackEvent))]
public partial class LiveMigrationJsonContext : JsonSerializerContext
{
}
