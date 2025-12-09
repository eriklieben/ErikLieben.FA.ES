namespace ErikLieben.FA.ES.Configuration;

/// <summary>
/// Provides a mapping from aggregate names to their designated storage connection names.
/// This allows projections and cross-aggregate queries to locate aggregates stored in different storage accounts.
/// </summary>
public interface IAggregateStorageRegistry
{
    /// <summary>
    /// Gets the storage connection name for the specified aggregate type.
    /// </summary>
    /// <param name="aggregateName">The aggregate name (e.g., "userprofile", "project").</param>
    /// <returns>The storage connection name if mapped; otherwise null.</returns>
    string? GetStorageForAggregate(string aggregateName);

    /// <summary>
    /// Determines whether a storage mapping exists for the specified aggregate.
    /// </summary>
    /// <param name="aggregateName">The aggregate name to check.</param>
    /// <returns>True if a mapping exists; otherwise false.</returns>
    bool HasMapping(string aggregateName);
}
