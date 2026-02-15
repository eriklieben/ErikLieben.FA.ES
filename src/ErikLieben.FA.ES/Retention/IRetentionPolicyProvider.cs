namespace ErikLieben.FA.ES.Retention;

/// <summary>
/// Provides retention policies for aggregate types.
/// </summary>
public interface IRetentionPolicyProvider
{
    /// <summary>
    /// Gets the retention policy for the specified aggregate type.
    /// </summary>
    /// <param name="aggregateType">The aggregate type.</param>
    /// <returns>The retention policy, or null if no policy applies.</returns>
    RetentionPolicy? GetPolicy(Type aggregateType);

    /// <summary>
    /// Gets the retention policy for the specified aggregate type.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <returns>The retention policy, or null if no policy applies.</returns>
    RetentionPolicy? GetPolicy<T>() where T : class;

    /// <summary>
    /// Registers a policy for a specific aggregate type, overriding any attribute-based policy.
    /// </summary>
    /// <param name="aggregateType">The aggregate type.</param>
    /// <param name="policy">The policy to register.</param>
    void RegisterPolicy(Type aggregateType, RetentionPolicy policy);

    /// <summary>
    /// Gets all registered aggregate types with retention policies.
    /// </summary>
    /// <returns>Collection of aggregate type names with policies.</returns>
    IEnumerable<string> GetRegisteredTypes();
}
