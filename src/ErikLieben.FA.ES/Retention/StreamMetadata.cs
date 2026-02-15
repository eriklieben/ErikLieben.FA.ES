namespace ErikLieben.FA.ES.Retention;

/// <summary>
/// Metadata about an event stream used for retention policy evaluation.
/// </summary>
/// <param name="ObjectName">The object type name.</param>
/// <param name="ObjectId">The object identifier.</param>
/// <param name="EventCount">The total number of events in the stream.</param>
/// <param name="OldestEventDate">The date of the oldest event, if available.</param>
/// <param name="NewestEventDate">The date of the newest event, if available.</param>
public record StreamMetadata(
    string ObjectName,
    string ObjectId,
    int EventCount,
    DateTimeOffset? OldestEventDate,
    DateTimeOffset? NewestEventDate);
