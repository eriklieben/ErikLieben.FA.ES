using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Registry tracking all destinations and their metadata in a routed projection.
/// </summary>
public class DestinationRegistry
{
    /// <summary>
    /// All known destinations, keyed by destination key.
    /// </summary>
    [JsonPropertyName("destinations")]
    public Dictionary<string, DestinationMetadata> Destinations { get; set; } = new();

    /// <summary>
    /// When the registry was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; set; }
}
