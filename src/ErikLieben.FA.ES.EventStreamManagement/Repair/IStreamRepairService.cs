using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.EventStreamManagement.Repair;

/// <summary>
/// Service for repairing broken event streams that have orphaned events from failed commits.
/// </summary>
public interface IStreamRepairService
{
    /// <summary>
    /// Repairs a broken stream by cleaning up orphaned events identified in the document's <see cref="BrokenStreamInfo"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses the <see cref="IObjectDocument.Active"/> stream's <see cref="StreamInformation.BrokenInfo"/>
    /// to determine which events need to be removed. After successful cleanup:
    /// </para>
    /// <list type="bullet">
    /// <item>The <see cref="StreamInformation.IsBroken"/> flag is cleared</item>
    /// <item>The <see cref="StreamInformation.BrokenInfo"/> is set to null</item>
    /// <item>A <see cref="RollbackRecord"/> is added to <see cref="StreamInformation.RollbackHistory"/></item>
    /// <item>The document metadata is persisted</item>
    /// </list>
    /// </remarks>
    /// <param name="document">The document with broken stream information.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The result of the repair operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the stream is not marked as broken.</exception>
    Task<StreamRepairResult> RepairBrokenStreamAsync(
        IObjectDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Repairs a broken stream by cleaning up orphaned events in the specified version range.
    /// </summary>
    /// <remarks>
    /// Use this overload when you know the exact version range to clean up but the document
    /// may not have <see cref="StreamInformation.BrokenInfo"/> set (e.g., manual recovery scenarios).
    /// </remarks>
    /// <param name="document">The document identifying the stream.</param>
    /// <param name="fromVersion">The first version to remove (inclusive).</param>
    /// <param name="toVersion">The last version to remove (inclusive).</param>
    /// <param name="reason">The reason for the repair operation.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The result of the repair operation.</returns>
    Task<StreamRepairResult> RepairBrokenStreamAsync(
        IObjectDocument document,
        int fromVersion,
        int toVersion,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends an <see cref="Events.EventsRolledBackEvent"/> marker to the stream for audit trail purposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: Appending a marker event advances the stream version. After this call:
    /// </para>
    /// <list type="bullet">
    /// <item>The marker becomes the last event in the stream</item>
    /// <item>Future business events will start at (marker version + 1)</item>
    /// <item>Retrying the original failed operation cannot use the same version numbers</item>
    /// </list>
    /// <para>
    /// Only use this if you want an explicit record in the event stream. The <see cref="RollbackRecord"/>
    /// in document metadata (automatically recorded by repair operations) provides
    /// an audit trail without affecting version numbering.
    /// </para>
    /// </remarks>
    /// <param name="document">The document identifying the stream.</param>
    /// <param name="rollbackRecord">The rollback record with repair details.</param>
    /// <param name="correlationId">Optional correlation ID for tracing.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the marker is appended.</returns>
    Task AppendRollbackMarkerAsync(
        IObjectDocument document,
        RollbackRecord rollbackRecord,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all documents with broken streams in the document store.
    /// </summary>
    /// <param name="objectName">Optional filter by object type name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An enumerable of documents with <see cref="StreamInformation.IsBroken"/> set to true.</returns>
    Task<IEnumerable<IObjectDocument>> FindBrokenStreamsAsync(
        string? objectName = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a stream repair operation.
/// </summary>
/// <param name="Success">Whether the repair completed successfully.</param>
/// <param name="EventsRemoved">Number of events removed from storage.</param>
/// <param name="RollbackRecord">The rollback record that was created and stored in metadata.</param>
/// <param name="ErrorMessage">Error message if the repair failed.</param>
public record StreamRepairResult(
    bool Success,
    int EventsRemoved,
    RollbackRecord? RollbackRecord,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a successful repair result.
    /// </summary>
    public static StreamRepairResult Succeeded(int eventsRemoved, RollbackRecord rollbackRecord)
        => new(true, eventsRemoved, rollbackRecord);

    /// <summary>
    /// Creates a failed repair result.
    /// </summary>
    public static StreamRepairResult Failed(string errorMessage)
        => new(false, 0, null, errorMessage);
}
