using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: UserProfile was created recently (within specified days)
/// </summary>
public sealed class RecentlyCreatedSpecification : Specification<Aggregates.UserProfile>
{
    private readonly int _daysAgo;

    public RecentlyCreatedSpecification(int daysAgo = 7)
    {
        if (daysAgo < 0)
            throw new ArgumentException("Days ago must be non-negative", nameof(daysAgo));
        _daysAgo = daysAgo;
    }

    public override bool IsSatisfiedBy(Aggregates.UserProfile entity)
    {
        if (entity == null) return false;

        var threshold = DateTime.UtcNow.AddDays(-_daysAgo);
        return entity.CreatedAt >= threshold;
    }
}
