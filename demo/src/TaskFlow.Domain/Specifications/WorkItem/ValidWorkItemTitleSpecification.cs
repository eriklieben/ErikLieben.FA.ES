using ErikLieben.FA.Specifications;

namespace TaskFlow.Domain.Specifications.WorkItem;

/// <summary>
/// Specification: WorkItem title is valid (not empty, between 5 and 200 characters)
/// </summary>
public sealed class ValidWorkItemTitleSpecification : Specification<string>
{
    public override bool IsSatisfiedBy(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        return title.Length >= 5 && title.Length <= 200;
    }
}
