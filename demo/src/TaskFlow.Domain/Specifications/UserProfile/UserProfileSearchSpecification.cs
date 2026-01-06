using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: UserProfile display name or email contains search term
/// </summary>
public sealed class UserProfileSearchSpecification : Specification<Aggregates.UserProfile>
{
    private readonly string _searchTerm;

    public UserProfileSearchSpecification(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            throw new ArgumentException("Search term cannot be empty", nameof(searchTerm));
        _searchTerm = searchTerm;
    }

    public override bool IsSatisfiedBy(Aggregates.UserProfile entity)
    {
        if (entity == null) return false;

        return (entity.DisplayName?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (entity.Email?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
