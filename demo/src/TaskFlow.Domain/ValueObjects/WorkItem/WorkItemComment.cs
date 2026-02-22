namespace TaskFlow.Domain.ValueObjects.WorkItem;

public record WorkItemComment(
    string FeedbackId,
    string Content,
    string ProvidedBy,
    DateTime ProvidedAt);
