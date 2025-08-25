using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES;

[JsonConverter(typeof(IEventToJsonEventConverter))]
public interface IEvent
{
    string? Payload { get; }
    string EventType { get; }
    int EventVersion { get; }
    string? ExternalSequencer { get; }

    ActionMetadata? ActionMetadata { get; }

    Dictionary<string, string> Metadata { get; }
}

public interface IEvent<out T> : IEventWithData where T : class
{
    new T Data();
}
