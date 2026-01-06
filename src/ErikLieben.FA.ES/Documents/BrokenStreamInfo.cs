namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Contains information about why a stream is in a broken state.
/// </summary>
/// <remarks>
/// A stream is marked as broken when a commit fails after events may have been
/// partially written, and the automatic cleanup also fails. This information
/// helps diagnose and repair the stream using the IStreamRepairService from the
/// ErikLieben.FA.ES.EventStreamManagement package.
/// </remarks>
public class BrokenStreamInfo
{
    /// <summary>
    /// Gets or sets when the stream was marked as broken.
    /// </summary>
    public DateTimeOffset BrokenAt { get; set; }

    /// <summary>
    /// Gets or sets the first version of potentially orphaned events.
    /// </summary>
    public int OrphanedFromVersion { get; set; }

    /// <summary>
    /// Gets or sets the last version of potentially orphaned events.
    /// </summary>
    public int OrphanedToVersion { get; set; }

    /// <summary>
    /// Gets or sets the error message from the failed cleanup attempt.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the type of the original exception that caused the commit failure.
    /// </summary>
    public string? OriginalExceptionType { get; set; }

    /// <summary>
    /// Gets or sets the type of the exception that caused the cleanup failure.
    /// </summary>
    public string? CleanupExceptionType { get; set; }
}
