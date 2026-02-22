using ErikLieben.FA.Specifications;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem is not currently in progress
/// </summary>
public sealed class NotInProgressWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem => workItem.Status != WorkItemStatus.InProgress);
