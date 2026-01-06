using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project has at least a minimum number of team members
/// </summary>
public sealed class MinimumTeamSizeSpecification : Specification<Aggregates.Project>
{
    private readonly int _minimumSize;

    public MinimumTeamSizeSpecification(int minimumSize)
    {
        if (minimumSize < 0)
            throw new ArgumentException("Minimum size must be non-negative", nameof(minimumSize));
        _minimumSize = minimumSize;
    }

    public override bool IsSatisfiedBy(Aggregates.Project entity)
    {
        return entity?.TeamMembers.Count >= _minimumSize;
    }
}
