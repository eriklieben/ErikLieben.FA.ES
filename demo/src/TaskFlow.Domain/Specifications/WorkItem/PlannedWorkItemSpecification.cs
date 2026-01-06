using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem is in Planned status
/// </summary>
public sealed class PlannedWorkItemSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        workItem?.Status == WorkItemStatus.Planned);
