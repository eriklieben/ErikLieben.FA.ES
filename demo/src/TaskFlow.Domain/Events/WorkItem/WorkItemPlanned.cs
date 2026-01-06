using ErikLieben.FA.ES.Attributes;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// New work item was planned for the project
/// </summary>
[EventName("WorkItem.Planned")]
public record WorkItemPlanned(
    string ProjectId,
    string Title,
    string Description,
    WorkItemPriority Priority,
    string PlannedBy,
    DateTime PlannedAt,
    Dictionary<string, string>? TitleTranslations = null);
