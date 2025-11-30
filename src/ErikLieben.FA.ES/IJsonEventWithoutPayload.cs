namespace ErikLieben.FA.ES;

/// <summary>
/// Defines the JSON-serializable shape of an event without a payload.
/// </summary>
public interface IJsonEventWithoutPayload
{
    /// <summary>
    /// Gets or sets metadata about the action that produced the event; null when not captured.
    /// </summary>
    ActionMetadata ActionMetadata { get; set; }

    /// <summary>
    /// Gets or sets the logical event type name used for routing and deserialization.
    /// </summary>
    string EventType { get; set; }

    /// <summary>
    /// Gets or sets the zero-based position of the event within its stream.
    /// </summary>
    int EventVersion { get; set; }

    /// <summary>
    /// Gets or sets the schema version of the event payload. Defaults to 1 when not specified.
    /// </summary>
    int SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the external sequencer identifier when the event is sequenced by an external system; otherwise null.
    /// </summary>
    string? ExternalSequencer { get; set; }

    /// <summary>
    /// Gets or sets additional key/value metadata associated with the event.
    /// </summary>
    Dictionary<string, string> Metadata { get; set; }
}
