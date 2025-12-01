namespace ErikLieben.FA.ES.EventStreamManagement.Cutover;

/// <summary>
/// Represents the different phases of a stream migration process.
/// </summary>
public enum MigrationPhase
{
    /// <summary>
    /// Normal operations - write to and read from the original stream only.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Dual-write phase - write to both old and new streams, read from old stream.
    /// This ensures the new stream catches up completely.
    /// </summary>
    DualWrite = 1,

    /// <summary>
    /// Dual-read phase - write to both streams, read from new stream with fallback to old.
    /// This verifies the new stream works correctly before full cutover.
    /// </summary>
    DualRead = 2,

    /// <summary>
    /// Cutover complete - read from and write to new stream only.
    /// ObjectDocument.Active has been updated to point to the new stream.
    /// </summary>
    Cutover = 3,

    /// <summary>
    /// Book closed - old stream has been archived and marked as terminated.
    /// </summary>
    BookClosed = 4
}
