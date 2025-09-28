namespace ErikLieben.FA.ES;

/// <summary>
/// Represents an event that exposes its payload as an untyped object in addition to the serialized form.
/// </summary>
public interface IEventWithData : IEvent
{
    /// <summary>
    /// Gets the event payload as an object; null when the event carries no payload.
    /// </summary>
    object? Data { get; }
}
