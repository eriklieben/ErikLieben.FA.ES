using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: UserProfile has been updated (not just created)
/// </summary>
public sealed class HasBeenUpdatedSpecification()
    : DelegateSpecification<Aggregates.UserProfile>(profile =>
        profile?.LastUpdatedAt.HasValue ?? false);
