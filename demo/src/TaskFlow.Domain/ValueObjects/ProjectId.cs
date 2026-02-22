using ErikLieben.FA.StronglyTypedIds;

namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a Project aggregate
/// </summary>
[GenerateStronglyTypedIdSupport]
public partial record ProjectId(Guid Value) : StronglyTypedId<Guid>(Value);
