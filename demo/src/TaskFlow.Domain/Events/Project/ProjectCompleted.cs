using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// LEGACY EVENT - Project work concluded and was archived (replaced by specific outcome events)
/// This event is kept for backwards compatibility and will be upcasted to specific outcome events
/// </summary>
[EventName("Project.Completed")]
public record ProjectCompleted(
    string Outcome,
    string CompletedBy,
    DateTime CompletedAt);
