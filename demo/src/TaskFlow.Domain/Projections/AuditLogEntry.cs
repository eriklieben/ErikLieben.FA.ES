using System.Text.Json.Serialization;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Document representing a single audit log entry in CosmosDB.
/// Each event creates a new audit log document.
/// </summary>
/// <remarks>
/// Schema:
/// - id: Auto-generated GUID
/// - partitionKey: WorkItemId (enables efficient per-item history queries)
///
/// This enables queries like:
/// - Get complete history of a work item
/// - Get all changes in a time range
/// - Filter by event type or user
/// </remarks>
public class AuditLogEntry
{
    /// <summary>
    /// Gets or sets the unique document ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the partition key (WorkItemId for efficient per-item queries).
    /// </summary>
    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the work item ID.
    /// </summary>
    [JsonPropertyName("workItemId")]
    public string WorkItemId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event type name.
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the event occurred.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets who triggered the event.
    /// </summary>
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of the change.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional event-specific data.
    /// </summary>
    [JsonPropertyName("details")]
    public Dictionary<string, object?>? Details { get; set; }
}
