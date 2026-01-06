namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when a commit operation fails in a leased session.
/// </summary>
/// <remarks>
/// This exception provides context about the commit state to help with recovery:
/// - The original version before the commit was attempted
/// - The version that was being committed to
/// - Whether events may have been written to storage despite the failure
/// </remarks>
public class CommitFailedException : EsException
{
    /// <summary>
    /// Gets the stream version before the commit was attempted.
    /// </summary>
    public int OriginalVersion { get; }

    /// <summary>
    /// Gets the stream version that the commit was attempting to reach.
    /// </summary>
    public int AttemptedVersion { get; }

    /// <summary>
    /// Gets a value indicating whether events may have been written to storage.
    /// </summary>
    /// <remarks>
    /// When true, some or all events may have been persisted to the data store
    /// even though the overall commit operation failed. This can happen when
    /// events are successfully appended but the document metadata update fails.
    /// </remarks>
    public bool EventsMayBeWritten { get; }

    /// <summary>
    /// Gets the stream identifier where the commit was attempted.
    /// </summary>
    public string? StreamIdentifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommitFailedException"/> class.
    /// </summary>
    /// <param name="streamIdentifier">The stream identifier where the commit was attempted.</param>
    /// <param name="originalVersion">The stream version before the commit was attempted.</param>
    /// <param name="attemptedVersion">The stream version that the commit was attempting to reach.</param>
    /// <param name="eventsMayBeWritten">Whether events may have been written to storage.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public CommitFailedException(
        string? streamIdentifier,
        int originalVersion,
        int attemptedVersion,
        bool eventsMayBeWritten,
        string message,
        Exception innerException)
        : base("ELFAES-COMMIT-0001", message, innerException)
    {
        StreamIdentifier = streamIdentifier;
        OriginalVersion = originalVersion;
        AttemptedVersion = attemptedVersion;
        EventsMayBeWritten = eventsMayBeWritten;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommitFailedException"/> class.
    /// </summary>
    /// <param name="streamIdentifier">The stream identifier where the commit was attempted.</param>
    /// <param name="originalVersion">The stream version before the commit was attempted.</param>
    /// <param name="attemptedVersion">The stream version that the commit was attempting to reach.</param>
    /// <param name="eventsMayBeWritten">Whether events may have been written to storage.</param>
    /// <param name="message">The error message.</param>
    public CommitFailedException(
        string? streamIdentifier,
        int originalVersion,
        int attemptedVersion,
        bool eventsMayBeWritten,
        string message)
        : base("ELFAES-COMMIT-0001", message)
    {
        StreamIdentifier = streamIdentifier;
        OriginalVersion = originalVersion;
        AttemptedVersion = attemptedVersion;
        EventsMayBeWritten = eventsMayBeWritten;
    }
}
