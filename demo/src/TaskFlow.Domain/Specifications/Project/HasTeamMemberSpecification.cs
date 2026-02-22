using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project has a specific team member
/// </summary>
public sealed class HasTeamMemberSpecification : Specification<Aggregates.Project>
{
    private readonly UserProfileId _memberId;

    public HasTeamMemberSpecification(UserProfileId memberId)
    {
        _memberId = memberId ?? throw new ArgumentNullException(nameof(memberId));
    }

    public override bool IsSatisfiedBy(Aggregates.Project entity)
    {
        return entity?.TeamMembers.ContainsKey(_memberId) ?? false;
    }
}
