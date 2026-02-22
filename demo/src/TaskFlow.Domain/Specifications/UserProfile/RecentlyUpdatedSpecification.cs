using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: UserProfile was recently updated (within specified days)
/// </summary>
public sealed class RecentlyUpdatedSpecification : Specification<Aggregates.UserProfile>
{
    private readonly int _daysAgo;

    public RecentlyUpdatedSpecification(int daysAgo = 7)
    {
        if (daysAgo < 0)
            throw new ArgumentException("Days ago must be non-negative", nameof(daysAgo));
        _daysAgo = daysAgo;
    }

    public override bool IsSatisfiedBy(Aggregates.UserProfile entity)
    {
        if (entity?.LastUpdatedAt == null) return false;

        var threshold = DateTime.UtcNow.AddDays(-_daysAgo);
        return entity.LastUpdatedAt.Value >= threshold;
    }
}
