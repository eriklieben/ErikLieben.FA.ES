using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: UserProfile has complete information (display name and email)
/// </summary>
public sealed class CompleteProfileSpecification()
    : DelegateSpecification<Aggregates.UserProfile>(profile =>
        !string.IsNullOrWhiteSpace(profile?.DisplayName) &&
        !string.IsNullOrWhiteSpace(profile?.Email) &&
        profile.Email.Contains('@'));
