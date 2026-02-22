using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: UserProfile has a display name set
/// </summary>
public sealed class HasDisplayNameSpecification()
    : DelegateSpecification<Aggregates.UserProfile>(profile =>
        !string.IsNullOrWhiteSpace(profile?.DisplayName));
