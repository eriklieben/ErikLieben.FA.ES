namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Result of discovering work items for catch-up.
/// Contains a page of work items along with pagination information.
/// </summary>
/// <param name="WorkItems">The work items discovered in this page.</param>
/// <param name="ContinuationToken">
/// Token for retrieving the next page of results.
/// Null when there are no more results.
/// </param>
/// <param name="TotalEstimate">
/// Optional estimate of total work items across all object types.
/// May be null if estimation was not requested or not available.
/// </param>
public record CatchUpDiscoveryResult(
    IReadOnlyList<CatchUpWorkItem> WorkItems,
    string? ContinuationToken,
    long? TotalEstimate);
