using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project name matches a pattern (case-insensitive)
/// </summary>
public sealed class ProjectNameContainsSpecification : Specification<Aggregates.Project>
{
    private readonly string _searchTerm;

    public ProjectNameContainsSpecification(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            throw new ArgumentException("Search term cannot be empty", nameof(searchTerm));
        _searchTerm = searchTerm;
    }

    public override bool IsSatisfiedBy(Aggregates.Project entity)
    {
        return entity?.Name?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
