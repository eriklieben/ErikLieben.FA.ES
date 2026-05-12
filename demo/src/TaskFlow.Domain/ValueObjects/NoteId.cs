using ErikLieben.FA.StronglyTypedIds;

namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a Note aggregate.
/// </summary>
[GenerateStronglyTypedIdSupport]
public partial record NoteId(string Value) : StronglyTypedId<string>(Value);
