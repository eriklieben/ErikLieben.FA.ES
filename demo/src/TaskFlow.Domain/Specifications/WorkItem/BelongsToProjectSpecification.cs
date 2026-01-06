using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem belongs to a specific project
/// </summary>
public sealed class BelongsToProjectSpecification : Specification<Aggregates.WorkItem>
{
    private readonly string _projectId;

    public BelongsToProjectSpecification(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project ID cannot be empty", nameof(projectId));
        _projectId = projectId;
    }

    public override bool IsSatisfiedBy(Aggregates.WorkItem entity)
    {
        return entity?.ProjectId == _projectId;
    }
}
