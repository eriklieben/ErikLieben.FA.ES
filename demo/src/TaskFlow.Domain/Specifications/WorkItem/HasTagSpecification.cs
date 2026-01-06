using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem has specific tag
/// </summary>
public sealed class HasTagSpecification : Specification<Aggregates.WorkItem>
{
    private readonly string _tag;

    public HasTagSpecification(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty", nameof(tag));
        _tag = tag;
    }

    public override bool IsSatisfiedBy(Aggregates.WorkItem entity)
    {
        return entity?.Tags.Contains(_tag, StringComparer.OrdinalIgnoreCase) ?? false;
    }
}
