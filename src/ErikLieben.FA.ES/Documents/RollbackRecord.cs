namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Records information about a successful rollback of partially written events.
/// </summary>
/// <remarks>
/// A rollback record is created when automatic cleanup successfully removes orphaned events
/// after a commit failure. This provides an audit trail in the document metadata without
/// affecting stream version numbering.
/// </remarks>
public class RollbackRecord
{
    /// <summary>
    /// Gets or sets when the rollback occurred.
    /// </summary>
    public DateTimeOffset RolledBackAt { get; set; }

    /// <summary>
    /// Gets or sets the first version of events that were removed.
    /// </summary>
    public int FromVersion { get; set; }

    /// <summary>
    /// Gets or sets the last version of events that were removed.
    /// </summary>
    public int ToVersion { get; set; }

    /// <summary>
    /// Gets or sets the number of events actually removed from storage.
    /// </summary>
    /// <remarks>
    /// This may be less than (ToVersion - FromVersion + 1) if some events
    /// were not written before the failure occurred.
    /// </remarks>
    public int EventsRemoved { get; set; }

    /// <summary>
    /// Gets or sets the error message from the original exception that caused the commit failure.
    /// </summary>
    public string? OriginalError { get; set; }

    /// <summary>
    /// Gets or sets the type name of the original exception that caused the commit failure.
    /// </summary>
    public string? OriginalExceptionType { get; set; }
}
