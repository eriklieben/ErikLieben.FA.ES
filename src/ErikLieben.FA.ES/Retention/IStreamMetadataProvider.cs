namespace ErikLieben.FA.ES.Retention;

/// <summary>
/// Provides stream metadata for retention policy evaluation.
/// </summary>
public interface IStreamMetadataProvider
{
    /// <summary>
    /// Gets metadata about a specific event stream.
    /// </summary>
    /// <param name="objectName">The object type name (e.g., "order").</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stream metadata, or null if the stream does not exist.</returns>
    Task<StreamMetadata?> GetStreamMetadataAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken = default);
}
