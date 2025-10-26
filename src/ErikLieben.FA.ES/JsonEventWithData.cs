namespace ErikLieben.FA.ES;

/// <summary>
/// Represents a JSON event that includes deserialized data.
/// </summary>
public record JsonEventWithData : JsonEvent, IEventWithData
{
    /// <summary>
    /// Gets or sets the deserialized event data.
    /// </summary>
    public object? Data { get; set; }
}
