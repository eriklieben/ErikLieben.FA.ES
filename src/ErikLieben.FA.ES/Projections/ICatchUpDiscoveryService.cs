namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Discovers work items for projection catch-up using <see cref="IObjectIdProvider"/>.
/// This service enumerates all object IDs for specified object types, allowing consumers
/// to orchestrate catch-up processing using their preferred mechanism (Durable Functions,
/// queues, batch processing, etc.).
/// </summary>
public interface ICatchUpDiscoveryService
{
    /// <summary>
    /// Discovers object IDs that need catch-up processing.
    /// Returns paginated results for handling large datasets.
    /// </summary>
    /// <param name="objectNames">
    /// The object type names to enumerate (e.g., "project", "workitem").
    /// </param>
    /// <param name="pageSize">
    /// The maximum number of work items to return per page. Default is 100.
    /// </param>
    /// <param name="continuationToken">
    /// Token from a previous call to continue enumeration. Null for the first page.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A <see cref="CatchUpDiscoveryResult"/> containing work items and a continuation token.
    /// </returns>
    Task<CatchUpDiscoveryResult> DiscoverWorkItemsAsync(
        string[] objectNames,
        int pageSize = 100,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all work items as an async enumerable.
    /// Useful for batch processing or when you want to process all items without pagination.
    /// </summary>
    /// <param name="objectNames">
    /// The object type names to enumerate (e.g., "project", "workitem").
    /// </param>
    /// <param name="pageSize">
    /// The page size for internal pagination. Default is 100.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// An async enumerable of <see cref="CatchUpWorkItem"/> instances.
    /// </returns>
    IAsyncEnumerable<CatchUpWorkItem> StreamWorkItemsAsync(
        string[] objectNames,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an estimate of total work items across all specified object types.
    /// </summary>
    /// <remarks>
    /// This may be expensive for large datasets as it requires counting objects
    /// in each storage provider. Use sparingly.
    /// </remarks>
    /// <param name="objectNames">
    /// The object type names to count (e.g., "project", "workitem").
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The estimated total number of work items.</returns>
    Task<long> EstimateTotalWorkItemsAsync(
        string[] objectNames,
        CancellationToken cancellationToken = default);
}
