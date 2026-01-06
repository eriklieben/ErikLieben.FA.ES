using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project is owned by a specific user
/// </summary>
public sealed class OwnedBySpecification : Specification<Aggregates.Project>
{
    private readonly UserProfileId _ownerId;

    public OwnedBySpecification(UserProfileId ownerId)
    {
        _ownerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
    }

    public override bool IsSatisfiedBy(Aggregates.Project entity)
    {
        return entity?.OwnerId == _ownerId;
    }
}
