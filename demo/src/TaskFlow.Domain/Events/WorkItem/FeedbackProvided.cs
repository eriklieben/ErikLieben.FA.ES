using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.WorkItem;

/// <summary>
/// Comment or discussion was added to work item
/// </summary>
[EventName("WorkItem.FeedbackProvided")]
public record FeedbackProvided(
    string FeedbackId,
    string Content,
    string ProvidedBy,
    DateTime ProvidedAt);
