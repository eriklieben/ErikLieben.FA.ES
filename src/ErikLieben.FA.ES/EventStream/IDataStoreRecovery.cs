using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Internal interface for dangerous data store operations that should only be used
/// for recovery from partial commit failures.
/// </summary>
/// <remarks>
/// <para>
/// This interface is intentionally internal to prevent misuse. Only the following
/// components should access these operations:
/// </para>
/// <list type="bullet">
/// <item><see cref="LeasedSession"/> - Automatic cleanup during commit failure</item>
/// <item>StreamRepairService - Manual repair of broken streams</item>
/// </list>
/// <para>
/// Implementations of <see cref="IDataStore"/> should also implement this interface
/// to support recovery operations.
/// </para>
/// </remarks>
internal interface IDataStoreRecovery
{
    /// <summary>
    /// Removes events written during a failed commit operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: This method violates event sourcing principles and should ONLY be used
    /// to clean up partially written events after a commit failure where events may
    /// have been written but the document update failed.
    /// </para>
    /// <para>
    /// This operation is idempotent - calling it multiple times with the same range
    /// will only remove events that exist.
    /// </para>
    /// </remarks>
    /// <param name="document">The document identifying the stream to clean up.</param>
    /// <param name="fromVersion">Start version (inclusive) of events to remove.</param>
    /// <param name="toVersion">End version (inclusive) of events to remove.</param>
    /// <returns>The number of events actually removed from storage.</returns>
    Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion);
}
