using ErikLieben.FA.StronglyTypedIds;

namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a WorkItem aggregate
/// </summary>
[GenerateStronglyTypedIdSupport]
public partial record WorkItemId(Guid Value) : StronglyTypedId<Guid>(Value);
