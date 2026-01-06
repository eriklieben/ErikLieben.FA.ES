using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Task ownership was given to a team member
/// </summary>
[EventName("WorkItem.ResponsibilityAssigned")]
public record ResponsibilityAssigned(
    string MemberId,
    string AssignedBy,
    DateTime AssignedAt);
