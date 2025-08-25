using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES;

public record ActionMetadata(
    [property:JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CorrelationId = null,
    [property:JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CausationId = null,
    [property:JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    VersionToken? OriginatedFromUser = null,
    [property:JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? EventOccuredAt = null)
{
} 