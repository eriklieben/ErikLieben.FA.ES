namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Represents a stream that has been terminated, including reason, date, and optional continuation metadata.
/// </summary>
public record TerminatedStream
{
    /// <summary>
    /// Gets or sets the identifier of the terminated stream.
    /// </summary>
    public string? StreamIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the stream provider type (e.g., "blob").
    /// </summary>
    public string? StreamType { get; set; }

    /// <summary>
    /// Gets or sets the connection name for the terminated stream.
    /// </summary>
    public string? StreamConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the reason for termination.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the identifier of a continuation stream when the stream continues elsewhere.
    /// </summary>
    public string? ContinuationStreamId { get; set; }

    /// <summary>
    /// Gets or sets the UTC date/time when the stream was terminated.
    /// </summary>
    public DateTimeOffset TerminationDate { get; set; }

    /// <summary>
    /// Gets or sets the stream version at termination, when known.
    /// </summary>
    public int? StreamVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the materialized document has been deleted.
    /// </summary>
    public bool Deleted { get; set; } = false;

    /// <summary>
    /// Gets or sets the UTC date/time when the document was deleted.
    /// </summary>
    public DateTimeOffset DeletionDate { get; set; }

    /// <summary>
    /// Gets or sets custom metadata associated with the terminated stream.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
