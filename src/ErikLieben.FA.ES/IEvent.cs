using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES;

/// <summary>
/// Represents a stored domain event with metadata and an optional serialized payload.
/// </summary>
/// <remarks>
/// The payload is stored as a JSON string and can be accessed via <see cref="IEventWithData"/> implementations to get a typed value.
/// </remarks>
[JsonConverter(typeof(IEventToJsonEventConverter))]
public interface IEvent
{
    /// <summary>
    /// Gets the JSON payload of the event; null when the event has no payload.
    /// </summary>
    string? Payload { get; }

    /// <summary>
    /// Gets the logical event type name used for routing and deserialization.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the zero-based position of the event within its stream.
    /// </summary>
    int EventVersion { get; }

    /// <summary>
    /// Gets the external sequencer identifier when the event is sequenced by an external system; otherwise null.
    /// </summary>
    string? ExternalSequencer { get; }

    /// <summary>
    /// Gets metadata about the action that produced the event; null when not captured.
    /// </summary>
    ActionMetadata? ActionMetadata { get; }

    /// <summary>
    /// Gets additional key/value metadata associated with the event.
    /// </summary>
    Dictionary<string, string> Metadata { get; }
}

/// <summary>
/// Represents a stored domain event with a typed payload.
/// </summary>
/// <typeparam name="T">The type of the event payload.</typeparam>
public interface IEvent<out T> : IEventWithData where T : class
{
    /// <summary>
    /// Gets the typed payload for the event.
    /// </summary>
    /// <returns>The payload cast to <typeparamref name="T"/>.</returns>
    new T Data();
}
