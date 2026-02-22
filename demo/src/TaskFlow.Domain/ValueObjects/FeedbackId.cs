using ErikLieben.FA.StronglyTypedIds;

namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for feedback/comments on work items
/// </summary>
[GenerateStronglyTypedIdSupport]
public partial record FeedbackId(Guid Value) : StronglyTypedId<Guid>(Value);
