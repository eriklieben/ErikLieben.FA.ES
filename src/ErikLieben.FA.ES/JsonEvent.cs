using ErikLieben.FA.ES.Exceptions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES;

/// <summary>
/// Represents an event stored in JSON format with metadata and payload.
/// </summary>
public record JsonEvent : IEvent, IJsonEventWithoutPayload
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonEvent"/> class.
    /// </summary>
    public JsonEvent() { }

    /// <summary>
    /// Gets or sets the JSON payload of the event.
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Gets or sets the type name of the event.
    /// </summary>
    [JsonPropertyName("type")]
    public required string EventType { get; set; }

    /// <summary>
    /// Gets or sets the version number of the event in the stream.
    /// </summary>
    [JsonPropertyName("version")]
    public required int EventVersion { get; set; }

    /// <summary>
    /// Gets or sets the external sequencer identifier for event ordering across streams.
    /// </summary>
    [JsonPropertyName("exseq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ExternalSequencer { get; set; }

    /// <summary>
    /// Gets or sets metadata about the action that triggered this event.
    /// </summary>
    [JsonPropertyName("action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ActionMetadata ActionMetadata { get; set; } = new ActionMetadata();

    /// <summary>
    /// Gets or sets additional metadata as key-value pairs.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Attempts to cast an event to a JsonEvent.
    /// </summary>
    /// <param name="event">The event to cast.</param>
    /// <returns>The event as a JsonEvent, or null if the cast fails.</returns>
    public static JsonEvent? From(IEvent @event)
    {
        return @event as JsonEvent;
    }

    /// <summary>
    /// Deserializes the event payload to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="event">The event containing the payload.</param>
    /// <param name="typeInfo">The JSON type information for deserialization.</param>
    /// <returns>The deserialized payload.</returns>
    /// <exception cref="ArgumentNullException">Thrown when event, payload, or typeInfo is null.</exception>
    /// <exception cref="UnableToDeserializeInTransitEventException">Thrown when deserialization fails.</exception>
    public static T To<T>(IEvent @event, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(@event?.Payload);
        ArgumentNullException.ThrowIfNull(typeInfo);

        return JsonSerializer.Deserialize(@event.Payload, typeInfo)
            ?? throw new UnableToDeserializeInTransitEventException();
    }

    /// <summary>
    /// Converts an event and data into a strongly-typed event.
    /// </summary>
    /// <typeparam name="T">The type of the event data.</typeparam>
    /// <param name="event">The source event.</param>
    /// <param name="data">The typed data.</param>
    /// <returns>A strongly-typed event.</returns>
    public static IEvent<T> ToEvent<T>(IEvent @event, T data) where T : class
    {
        return new Event<T>
        {
            EventType = @event.EventType,
            EventVersion = @event.EventVersion,
            ExternalSequencer = @event.ExternalSequencer,
            Data = data,
            Payload = @event.Payload,
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }

    /// <summary>
    /// Converts an event into a strongly-typed event by deserializing its payload.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the payload to.</typeparam>
    /// <param name="event">The source event.</param>
    /// <param name="typeInfo">The JSON type information for deserialization.</param>
    /// <returns>A strongly-typed event with deserialized data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when event or typeInfo is null.</exception>
    /// <exception cref="UnableToDeserializeInTransitEventException">Thrown when deserialization fails.</exception>
    public static IEvent<T> ToEvent<T>(IEvent @event, JsonTypeInfo<T> typeInfo) where T : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(typeInfo);

        var obj = JsonSerializer.Deserialize(@event.Payload, typeInfo)
            ?? throw new UnableToDeserializeInTransitEventException();

        return new Event<T>
        {
            EventType = @event.EventType,
            EventVersion = @event.EventVersion,
            ExternalSequencer = @event.ExternalSequencer,
            Data = obj,
            Payload = @event.Payload,
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}
