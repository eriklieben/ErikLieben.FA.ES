using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem has a deadline set
/// </summary>
public sealed class HasDeadlineSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        workItem?.Deadline.HasValue ?? false);
