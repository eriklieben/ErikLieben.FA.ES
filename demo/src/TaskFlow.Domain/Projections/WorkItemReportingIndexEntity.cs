using Azure;
using Azure.Data.Tables;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Table entity representing a work item in the reporting index.
/// Each work item is stored as a separate row in Azure Table Storage.
/// </summary>
/// <remarks>
/// Schema:
/// - PartitionKey: ProjectId (enables efficient project-scoped queries)
/// - RowKey: WorkItemId (unique identifier within project)
///
/// This enables queries like:
/// - Get all work items for a project (filter by PartitionKey)
/// - Get a specific work item (PartitionKey + RowKey)
/// - Filter by Status, Priority, AssignedTo across all projects
/// </remarks>
public class WorkItemReportingIndexEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (ProjectId).
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key (WorkItemId).
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the ETag.
    /// </summary>
    public ETag ETag { get; set; }

    /// <summary>
    /// Gets or sets the work item title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the work item description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assigned team member ID.
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Gets or sets when the work item was planned.
    /// </summary>
    public DateTime PlannedAt { get; set; }

    /// <summary>
    /// Gets or sets when work commenced.
    /// </summary>
    public DateTime? CommencedAt { get; set; }

    /// <summary>
    /// Gets or sets when work was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the deadline.
    /// </summary>
    public DateTime? Deadline { get; set; }

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets who last updated the work item.
    /// </summary>
    public string? LastUpdatedBy { get; set; }
}
