using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Work item scope and requirements were clarified
/// </summary>
[EventName("WorkItem.RequirementsRefined")]
public record RequirementsRefined(
    string NewDescription,
    string RefinedBy,
    DateTime RefinedAt);
