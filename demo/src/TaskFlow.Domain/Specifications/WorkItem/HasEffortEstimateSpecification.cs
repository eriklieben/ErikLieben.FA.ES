using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem has estimated hours
/// </summary>
public sealed class HasEffortEstimateSpecification()
    : DelegateSpecification<Aggregates.WorkItem>(workItem =>
        workItem?.EstimatedHours.HasValue ?? false);
