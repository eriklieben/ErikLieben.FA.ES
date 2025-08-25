namespace ErikLieben.FA.ES;

public interface IJsonEventWithoutPayload
{
    ActionMetadata ActionMetadata { get; set; }
    string EventType { get; set; }
    int EventVersion { get; set; }
    string? ExternalSequencer { get; set; }
    Dictionary<string, string> Metadata { get; set; }
}
