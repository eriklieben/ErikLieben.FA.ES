using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Metadata container for routed projections, serialized under "$metadata" property.
/// Contains the destination registry tracking all routed destinations.
/// </summary>
public class RoutedProjectionMetadata
{
    /// <summary>
    /// Registry tracking all destinations and their metadata.
    /// </summary>
    [JsonPropertyName("registry")]
    public DestinationRegistry Registry { get; set; } = new();
}
