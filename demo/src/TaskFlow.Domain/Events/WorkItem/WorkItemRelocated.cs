using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Task was moved to a different project
/// </summary>
[EventName("WorkItem.Relocated")]
public record WorkItemRelocated(
    string FormerProjectId,
    string NewProjectId,
    string Rationale,
    string RelocatedBy,
    DateTime RelocatedAt);
