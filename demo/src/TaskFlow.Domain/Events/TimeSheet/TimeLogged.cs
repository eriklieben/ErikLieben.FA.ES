using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.TimeSheet;

/// <summary>
/// Time was logged against a work item
/// </summary>
[EventName("TimeSheet.TimeLogged")]
public record TimeLogged(
    string EntryId,
    string WorkItemId,
    decimal Hours,
    string Description,
    DateTime Date,
    DateTime LoggedAt);
