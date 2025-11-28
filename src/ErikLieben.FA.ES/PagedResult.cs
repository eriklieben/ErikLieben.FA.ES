namespace ErikLieben.FA.ES;

/// <summary>
/// Represents a page of results with continuation token support for efficient pagination.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The items in the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>
    /// The number of items requested for this page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Continuation token for retrieving the next page. Null if no more pages.
    /// This is an opaque string provided by the storage provider and should not be parsed or manipulated.
    /// </summary>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// Indicates whether there are more pages available.
    /// </summary>
    public bool HasNextPage => !string.IsNullOrWhiteSpace(ContinuationToken);
}
