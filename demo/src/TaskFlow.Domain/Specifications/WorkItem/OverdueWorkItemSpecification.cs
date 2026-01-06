using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem deadline is overdue
/// </summary>
public sealed class OverdueWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        workItem?.Deadline.HasValue == true &&
        workItem.Deadline.Value < DateTime.UtcNow &&
        workItem.Status != WorkItemStatus.Completed);
