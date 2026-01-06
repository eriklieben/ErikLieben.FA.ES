using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem deadline is approaching (within specified days)
/// </summary>
public sealed class DeadlineApproachingSpecification : Specification<Aggregates.WorkItem>
{
    private readonly int _daysAhead;

    public DeadlineApproachingSpecification(int daysAhead = 3)
    {
        if (daysAhead < 0)
            throw new ArgumentException("Days ahead must be non-negative", nameof(daysAhead));
        _daysAhead = daysAhead;
    }

    public override bool IsSatisfiedBy(Aggregates.WorkItem entity)
    {
        if (entity?.Deadline == null || entity.Status == WorkItemStatus.Completed)
            return false;

        var threshold = DateTime.UtcNow.AddDays(_daysAhead);
        return entity.Deadline.Value <= threshold && entity.Deadline.Value >= DateTime.UtcNow;
    }
}
