using ErikLieben.FA.ES.Exceptions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES;

public record JsonEvent : IEvent, IJsonEventWithoutPayload
{
    public JsonEvent() { }

    public string? Payload { get; set; }

    [JsonPropertyName("type")]
    public required string EventType { get; set; }

    [JsonPropertyName("version")]
    public required int EventVersion { get; set; }

    [JsonPropertyName("exseq")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ExternalSequencer { get; set; }

    [JsonPropertyName("action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ActionMetadata ActionMetadata { get; set; } = new ActionMetadata();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<string, string> Metadata { get; set; } = [];

    public static JsonEvent? From(IEvent @event)
    {
        return @event as JsonEvent;
    }

    public static T To<T>(IEvent @event, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(@event?.Payload);
        ArgumentNullException.ThrowIfNull(typeInfo);

        return JsonSerializer.Deserialize(@event.Payload, typeInfo)
            ?? throw new UnableToDeserializeInTransitEventException();
    }

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
