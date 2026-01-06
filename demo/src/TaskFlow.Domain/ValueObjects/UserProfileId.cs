using ErikLieben.FA.StronglyTypedIds;

namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a user profile
/// </summary>
[GenerateStronglyTypedIdSupport]
public partial record UserProfileId(string Value) : StronglyTypedId<string>(Value);
