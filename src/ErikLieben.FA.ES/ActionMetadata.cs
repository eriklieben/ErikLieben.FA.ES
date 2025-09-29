using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES;

/// <summary>
/// Represents optional metadata about the action that produced an event.
/// </summary>
/// <param name="CorrelationId">An identifier used to correlate related operations across boundaries; null when not supplied.</param>
/// <param name="CausationId">An identifier that points to the originating action or command; null when not supplied.</param>
/// <param name="OriginatedFromUser">The version token of the user action that initiated the event; null when the origin is system-driven.</param>
/// <param name="EventOccuredAt">The timestamp (UTC) when the event occurred; null when not captured.</param>
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
