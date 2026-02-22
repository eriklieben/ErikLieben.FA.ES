namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when a commit operation fails and the automatic cleanup of partially
/// written events also fails, leaving the stream in a broken state.
/// </summary>
/// <remarks>
/// <para>
/// This exception indicates a serious error condition where:
/// 1. The original commit operation failed after potentially writing some events
/// 2. The automatic cleanup attempt also failed
/// 3. The stream is now marked as broken and requires manual intervention
/// </para>
/// <para>
/// Use <see cref="CleanupFromVersion"/> and <see cref="CleanupToVersion"/> to determine
/// which events may need to be manually removed. The stream can be repaired using
/// IStreamRepairService from the ErikLieben.FA.ES.EventStreamManagement package.
/// </para>
/// </remarks>
public class CommitCleanupFailedException : EsException
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
    /// Gets the stream identifier where the commit was attempted.
    /// </summary>
    public string? StreamIdentifier { get; }

    /// <summary>
    /// Gets the first version that needs cleanup.
    /// </summary>
    public int CleanupFromVersion { get; }

    /// <summary>
    /// Gets the last version that needs cleanup.
    /// </summary>
    public int CleanupToVersion { get; }

    /// <summary>
    /// Gets the exception that occurred during the cleanup attempt.
    /// </summary>
    public Exception CleanupException { get; }

    /// <summary>
    /// Gets the original exception that caused the commit to fail.
    /// </summary>
    public Exception OriginalCommitException { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommitCleanupFailedException"/> class.
    /// </summary>
    /// <param name="streamIdentifier">The stream identifier where the commit was attempted.</param>
    /// <param name="originalVersion">The stream version before the commit was attempted.</param>
    /// <param name="attemptedVersion">The stream version that the commit was attempting to reach.</param>
    /// <param name="cleanupFromVersion">The first version that needs cleanup.</param>
    /// <param name="cleanupToVersion">The last version that needs cleanup.</param>
    /// <param name="cleanupException">The exception that occurred during cleanup.</param>
    /// <param name="originalException">The original exception that caused the commit to fail.</param>
    public CommitCleanupFailedException(
        string? streamIdentifier,
        int originalVersion,
        int attemptedVersion,
        int cleanupFromVersion,
        int cleanupToVersion,
        Exception cleanupException,
        Exception originalException)
        : base(
            "ELFAES-COMMIT-0002",
            $"Commit failed and automatic cleanup also failed. " +
            $"Stream '{streamIdentifier}' is in broken state. " +
            $"Events {cleanupFromVersion}-{cleanupToVersion} may need manual cleanup. " +
            $"Original error: {originalException.Message}. " +
            $"Cleanup error: {cleanupException.Message}",
            originalException)
    {
        StreamIdentifier = streamIdentifier;
        OriginalVersion = originalVersion;
        AttemptedVersion = attemptedVersion;
        CleanupFromVersion = cleanupFromVersion;
        CleanupToVersion = cleanupToVersion;
        CleanupException = cleanupException;
        OriginalCommitException = originalException;
    }
}
