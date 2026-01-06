using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem has high priority
/// </summary>
public sealed class HighPriorityWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        workItem?.Priority == WorkItemPriority.High);
