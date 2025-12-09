namespace ErikLieben.FA.ES;

/// <summary>
/// Provides object ID enumeration and querying capabilities across storage providers.
/// </summary>
public interface IObjectIdProvider
{
    /// <summary>
    /// Gets a page of object IDs for the specified object type.
    /// </summary>
    /// <param name="objectName">The object type name (e.g., "project", "workItem").</param>
    /// <param name="continuationToken">Optional continuation token from previous page. Pass null for first page.</param>
    /// <param name="pageSize">Number of items to return. Maximum value is provider-dependent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result with items and continuation token for the next page.</returns>
    Task<PagedResult<string>> GetObjectIdsAsync(
        string objectName,
        string? continuationToken,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an object document exists for the given ID.
    /// </summary>
    /// <param name="objectName">The object type name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the object exists, false otherwise.</returns>
    Task<bool> ExistsAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of objects for the given type.
    /// Warning: This may be expensive for large datasets as it requires enumerating all items.
    /// </summary>
    /// <param name="objectName">The object type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total count of objects.</returns>
    Task<long> CountAsync(
        string objectName,
        CancellationToken cancellationToken = default);
}
