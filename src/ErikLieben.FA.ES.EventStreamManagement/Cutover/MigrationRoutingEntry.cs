namespace ErikLieben.FA.ES.EventStreamManagement.Cutover;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a routing entry stored in the routing table.
/// </summary>
public class MigrationRoutingEntry
{
    /// <summary>
    /// Gets or sets the object identifier.
    /// </summary>
    [JsonPropertyName("objectId")]
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current migration phase.
    /// </summary>
    [JsonPropertyName("phase")]
    public MigrationPhase Phase { get; set; }

    /// <summary>
    /// Gets or sets the old stream identifier.
    /// </summary>
    [JsonPropertyName("oldStream")]
    public string OldStream { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new stream identifier.
    /// </summary>
    [JsonPropertyName("newStream")]
    public string NewStream { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this routing was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when this routing was last updated.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the migration ID associated with this routing.
    /// </summary>
    [JsonPropertyName("migrationId")]
    public Guid MigrationId { get; set; }

    /// <summary>
    /// Converts this entry to a StreamRouting record.
    /// </summary>
    public StreamRouting ToStreamRouting()
    {
        return Phase switch
        {
            MigrationPhase.Normal => StreamRouting.Normal(OldStream),
            MigrationPhase.DualWrite => StreamRouting.DualWrite(OldStream, NewStream),
            MigrationPhase.DualRead => StreamRouting.DualRead(OldStream, NewStream),
            MigrationPhase.Cutover => StreamRouting.Cutover(NewStream),
            MigrationPhase.BookClosed => StreamRouting.Cutover(NewStream),
            _ => StreamRouting.Normal(OldStream)
        };
    }
}
