using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem is assigned to a team member
/// </summary>
public sealed class AssignedWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        !string.IsNullOrWhiteSpace(workItem?.AssignedTo));
