using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem is ready to start (Planned status, assigned, has estimate)
/// </summary>
public sealed class ReadyToStartSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        workItem?.Status == WorkItemStatus.Planned &&
        !string.IsNullOrWhiteSpace(workItem.AssignedTo) &&
        workItem.EstimatedHours.HasValue);
