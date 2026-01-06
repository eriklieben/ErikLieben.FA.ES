using ErikLieben.FA.Specifications;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem is in an active (not completed) state
/// </summary>
public sealed class ActiveWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem => workItem.Status != WorkItemStatus.Completed);
