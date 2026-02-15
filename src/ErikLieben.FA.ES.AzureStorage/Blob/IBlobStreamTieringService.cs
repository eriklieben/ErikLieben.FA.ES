using Azure.Storage.Blobs.Models;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Azure Blob Storage specific extensions for stream tiering.
/// </summary>
/// <remarks>
/// <para>
/// This service provides programmatic control over blob access tiers for event streams.
/// For general tiering, consider using Azure Blob Storage lifecycle management policies instead.
/// </para>
/// <para>
/// Common use cases:
/// - Tier closed/migrated streams to Archive after book closing
/// - Rehydrate archived streams before reading
/// - Move inactive streams to Cool tier to reduce costs
/// </para>
/// </remarks>
public interface IBlobStreamTieringService
{
    /// <summary>
    /// Changes the access tier for all blobs in a stream.
    /// </summary>
    /// <param name="objectName">The object/aggregate type name.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="tier">Target access tier (Hot, Cool, Cold, Archive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tiering result.</returns>
    Task<TieringResult> SetStreamTierAsync(
        string objectName,
        string streamId,
        AccessTier tier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current access tier for a stream.
    /// </summary>
    /// <param name="objectName">The object/aggregate type name.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current access tier.</returns>
    Task<AccessTier?> GetStreamTierAsync(
        string objectName,
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rehydrates an archived stream to the specified tier.
    /// Required before reading from Archive tier.
    /// </summary>
    /// <param name="objectName">The object/aggregate type name.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="priority">Standard (up to 15 hours) or High (up to 1 hour).</param>
    /// <param name="targetTier">The tier to rehydrate to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rehydration result.</returns>
    Task<RehydrationResult> RehydrateStreamAsync(
        string objectName,
        string streamId,
        RehydratePriority priority = RehydratePriority.Standard,
        AccessTier? targetTier = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a tiering operation.
/// </summary>
/// <param name="StreamId">The stream identifier.</param>
/// <param name="PreviousTier">The tier before the operation.</param>
/// <param name="NewTier">The new tier after the operation.</param>
/// <param name="BlobsProcessed">Number of blobs whose tier was changed.</param>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Error">Error message if failed.</param>
public record TieringResult(
    string StreamId,
    AccessTier? PreviousTier,
    AccessTier NewTier,
    int BlobsProcessed,
    bool Success,
    string? Error)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TieringResult Succeeded(
        string streamId,
        AccessTier? previousTier,
        AccessTier newTier,
        int blobsProcessed)
        => new(streamId, previousTier, newTier, blobsProcessed, true, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static TieringResult Failed(string streamId, string error)
        => new(streamId, null, default, 0, false, error);
}

/// <summary>
/// Result of a rehydration operation.
/// </summary>
/// <param name="StreamId">The stream identifier.</param>
/// <param name="Initiated">Whether rehydration was initiated.</param>
/// <param name="EstimatedDuration">Estimated time to complete rehydration.</param>
/// <param name="Error">Error message if failed.</param>
public record RehydrationResult(
    string StreamId,
    bool Initiated,
    TimeSpan? EstimatedDuration,
    string? Error)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static RehydrationResult Succeeded(string streamId, TimeSpan estimatedDuration)
        => new(streamId, true, estimatedDuration, null);

    /// <summary>
    /// Creates a result indicating no rehydration was needed.
    /// </summary>
    public static RehydrationResult NotNeeded(string streamId)
        => new(streamId, false, null, "Stream is not in Archive tier");

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static RehydrationResult Failed(string streamId, string error)
        => new(streamId, false, null, error);
}

/// <summary>
/// Priority for rehydration operations.
/// </summary>
public enum RehydratePriority
{
    /// <summary>
    /// Standard priority - up to 15 hours.
    /// </summary>
    Standard,

    /// <summary>
    /// High priority - up to 1 hour (higher cost).
    /// </summary>
    High
}
