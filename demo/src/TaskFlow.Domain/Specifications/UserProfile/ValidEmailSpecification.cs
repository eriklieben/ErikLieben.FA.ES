using ErikLieben.FA.Specifications;

namespace TaskFlow.Domain.Specifications.UserProfile;

/// <summary>
/// Specification: Email address has a valid format
/// </summary>
public sealed class ValidEmailSpecification()
    : DelegateSpecification<string>(email =>
        !string.IsNullOrWhiteSpace(email) &&
        email.Contains('@') &&
        email.Contains('.'));
