namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Configuration settings for event stream behavior.
/// </summary>
public class EventStreamSettings : IEventStreamSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether manual folding is enabled.
    /// When true, events are not automatically folded into aggregates during read operations.
    /// </summary>
    public bool ManualFolding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an external sequencer should be used for event ordering.
    /// </summary>
    public bool UseExternalSequencer { get; set; }
}
