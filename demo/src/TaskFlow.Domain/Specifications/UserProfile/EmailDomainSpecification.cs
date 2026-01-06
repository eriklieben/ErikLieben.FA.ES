using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: UserProfile email matches specific domain
/// </summary>
public sealed class EmailDomainSpecification : Specification<Aggregates.UserProfile>
{
    private readonly string _domain;

    public EmailDomainSpecification(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain cannot be empty", nameof(domain));
        _domain = domain.ToLowerInvariant();
    }

    public override bool IsSatisfiedBy(Aggregates.UserProfile entity)
    {
        if (string.IsNullOrWhiteSpace(entity?.Email)) return false;

        var parts = entity.Email.Split('@');
        if (parts.Length != 2) return false;

        return parts[1].Equals(_domain, StringComparison.OrdinalIgnoreCase);
    }
}
