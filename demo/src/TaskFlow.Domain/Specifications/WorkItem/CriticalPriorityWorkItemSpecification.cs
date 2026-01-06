using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem has critical priority
/// </summary>
public sealed class CriticalPriorityWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        workItem?.Priority == WorkItemPriority.Critical);
