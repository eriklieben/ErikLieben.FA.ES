namespace ErikLieben.FA.ES.Configuration;

/// <summary>
/// Default implementation of <see cref="IAggregateStorageRegistry"/> that uses a dictionary-based lookup.
/// </summary>
public class AggregateStorageRegistry : IAggregateStorageRegistry
{
    private readonly IReadOnlyDictionary<string, string> _storageMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateStorageRegistry"/> class.
    /// </summary>
    /// <param name="storageMap">A dictionary mapping aggregate names (lowercase) to storage connection names.</param>
    public AggregateStorageRegistry(IReadOnlyDictionary<string, string> storageMap)
    {
        ArgumentNullException.ThrowIfNull(storageMap);
        _storageMap = storageMap;
    }

    /// <summary>
    /// Gets the storage connection name for the specified aggregate type.
    /// </summary>
    /// <param name="aggregateName">The aggregate name (e.g., "userprofile", "project").</param>
    /// <returns>The storage connection name if mapped; otherwise null.</returns>
    public string? GetStorageForAggregate(string aggregateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateName);

        var normalizedName = aggregateName.ToLowerInvariant();
        return _storageMap.TryGetValue(normalizedName, out var storage) ? storage : null;
    }

    /// <summary>
    /// Determines whether a storage mapping exists for the specified aggregate.
    /// </summary>
    /// <param name="aggregateName">The aggregate name to check.</param>
    /// <returns>True if a mapping exists; otherwise false.</returns>
    public bool HasMapping(string aggregateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateName);

        var normalizedName = aggregateName.ToLowerInvariant();
        return _storageMap.ContainsKey(normalizedName);
    }
}
