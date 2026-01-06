using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Sprint;

/// <summary>
/// A sprint's goal was updated
/// </summary>
[EventName("Sprint.GoalUpdated")]
public record SprintGoalUpdated(
    string? PreviousGoal,
    string? NewGoal,
    string UpdatedBy,
    DateTime UpdatedAt);
