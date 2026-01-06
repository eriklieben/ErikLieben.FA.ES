using ErikLieben.FA.Specifications;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: Display name is valid (not empty, between 2 and 100 characters)
/// </summary>
public sealed class ValidDisplayNameSpecification : Specification<string>
{
    public override bool IsSatisfiedBy(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return false;

        return displayName.Length >= 2 && displayName.Length <= 100;
    }
}
