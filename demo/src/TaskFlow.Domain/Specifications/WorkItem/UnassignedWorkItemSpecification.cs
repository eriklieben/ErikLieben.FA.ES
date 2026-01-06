using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem is unassigned
/// </summary>
public sealed class UnassignedWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        string.IsNullOrWhiteSpace(workItem?.AssignedTo));
