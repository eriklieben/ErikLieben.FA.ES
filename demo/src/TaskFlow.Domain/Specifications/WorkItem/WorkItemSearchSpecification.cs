using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem title or description contains search term
/// </summary>
public sealed class WorkItemSearchSpecification : Specification<Aggregates.WorkItem>
{
    private readonly string _searchTerm;

    public WorkItemSearchSpecification(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            throw new ArgumentException("Search term cannot be empty", nameof(searchTerm));
        _searchTerm = searchTerm;
    }

    public override bool IsSatisfiedBy(Aggregates.WorkItem entity)
    {
        if (entity == null) return false;

        return (entity.Title?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (entity.Description?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
