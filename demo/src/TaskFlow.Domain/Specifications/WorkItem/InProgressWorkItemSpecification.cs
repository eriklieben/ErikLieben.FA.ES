using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem is in InProgress status
/// </summary>
public sealed class InProgressWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        workItem?.Status == WorkItemStatus.InProgress);
