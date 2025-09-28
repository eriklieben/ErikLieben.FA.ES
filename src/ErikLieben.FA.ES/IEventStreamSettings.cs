namespace ErikLieben.FA.ES;

/// <summary>
/// Defines settings that control the behavior of an <see cref="IEventStream"/>.
/// </summary>
public interface IEventStreamSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the aggregate performs manual folding instead of automatic folding during read operations.
    /// </summary>
    bool ManualFolding { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an external sequencer is used when reading events.
    /// </summary>
    bool UseExternalSequencer { get; set; }
}
