namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Provides snapshot policies for aggregate types.
/// </summary>
public interface ISnapshotPolicyProvider
{
    /// <summary>
    /// Gets the snapshot policy for the specified aggregate type.
    /// </summary>
    /// <param name="aggregateType">The aggregate type.</param>
    /// <returns>The snapshot policy, or null if no policy applies.</returns>
    SnapshotPolicy? GetPolicy(Type aggregateType);

    /// <summary>
    /// Gets the snapshot policy for the specified aggregate type.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <returns>The snapshot policy, or null if no policy applies.</returns>
    SnapshotPolicy? GetPolicy<T>() where T : class;

    /// <summary>
    /// Registers a policy for a specific aggregate type, overriding any attribute-based policy.
    /// </summary>
    /// <param name="aggregateType">The aggregate type.</param>
    /// <param name="policy">The policy to register.</param>
    void RegisterPolicy(Type aggregateType, SnapshotPolicy policy);
}
