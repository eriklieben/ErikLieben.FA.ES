namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Metadata about a destination in a routed projection.
/// The destination key is the dictionary key in Registry.Destinations, not stored here.
/// </summary>
public class DestinationMetadata
{
    /// <summary>
    /// Type name of the destination projection (for reconstruction).
    /// </summary>
    public string DestinationTypeName { get; set; } = string.Empty;

    /// <summary>
    /// When this destination was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When this destination was last modified.
    /// </summary>
    public DateTimeOffset LastModified { get; set; }

    /// <summary>
    /// Checkpoint fingerprint for this destination.
    /// </summary>
    public string? CheckpointFingerprint { get; set; }

    /// <summary>
    /// Storage provider-specific metadata (e.g., blob path, table name).
    /// This is managed by the storage factory, not by user code.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// User-provided metadata for path template resolution and custom data storage.
    /// </summary>
    public Dictionary<string, string> UserMetadata { get; set; } = new();
}
