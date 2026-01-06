using ErikLieben.FA.StronglyTypedIds;

namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for an Epic aggregate
/// </summary>
[GenerateStronglyTypedIdSupport]
public partial record EpicId(Guid Value) : StronglyTypedId<Guid>(Value);
