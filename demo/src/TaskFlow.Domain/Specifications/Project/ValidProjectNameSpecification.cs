using ErikLieben.FA.Specifications;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project name is valid (not empty, between 3 and 100 characters)
/// </summary>
public sealed class ValidProjectNameSpecification : Specification<string>
{
    public override bool IsSatisfiedBy(string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            return false;

        return projectName.Length >= 3 && projectName.Length <= 100;
    }
}
