using System.Collections.Concurrent;
using System.Reflection;
using ErikLieben.FA.ES.Attributes;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Default implementation of <see cref="ISnapshotPolicyProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Policy resolution order:
/// 1. Explicitly registered policies via <see cref="RegisterPolicy"/>
/// 2. Configuration overrides from <see cref="SnapshotOptions.PolicyOverrides"/>
/// 3. <see cref="SnapshotPolicyAttribute"/> on the aggregate type
/// 4. <see cref="SnapshotOptions.DefaultPolicy"/> if set
/// 5. null (no automatic snapshots)
/// </para>
/// </remarks>
public class SnapshotPolicyProvider : ISnapshotPolicyProvider
{
    private readonly ConcurrentDictionary<Type, SnapshotPolicy?> _policyCache = new();
    private readonly ConcurrentDictionary<Type, SnapshotPolicy> _registeredPolicies = new();
    private readonly SnapshotOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotPolicyProvider"/> class.
    /// </summary>
    /// <param name="options">The snapshot options.</param>
    public SnapshotPolicyProvider(IOptions<SnapshotOptions> options)
    {
        _options = options?.Value ?? SnapshotOptions.Default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotPolicyProvider"/> class
    /// with the specified options directly.
    /// </summary>
    /// <param name="options">The snapshot options.</param>
    public SnapshotPolicyProvider(SnapshotOptions options)
    {
        _options = options ?? SnapshotOptions.Default;
    }

    /// <inheritdoc />
    public SnapshotPolicy? GetPolicy(Type aggregateType)
    {
        ArgumentNullException.ThrowIfNull(aggregateType);

        return _policyCache.GetOrAdd(aggregateType, ResolvePolicy);
    }

    /// <inheritdoc />
    public SnapshotPolicy? GetPolicy<T>() where T : class
        => GetPolicy(typeof(T));

    /// <inheritdoc />
    public void RegisterPolicy(Type aggregateType, SnapshotPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(aggregateType);
        ArgumentNullException.ThrowIfNull(policy);

        _registeredPolicies[aggregateType] = policy;
        // Invalidate cache for this type
        _policyCache.TryRemove(aggregateType, out _);
    }

    private SnapshotPolicy? ResolvePolicy(Type aggregateType)
    {
        // 1. Check explicitly registered policies
        if (_registeredPolicies.TryGetValue(aggregateType, out var registered))
        {
            return registered;
        }

        // 2. Check configuration overrides
        var typeName = aggregateType.FullName ?? aggregateType.Name;
        if (_options.PolicyOverrides.TryGetValue(typeName, out var configOverride))
        {
            return configOverride;
        }

        // Also check short name
        if (_options.PolicyOverrides.TryGetValue(aggregateType.Name, out configOverride))
        {
            return configOverride;
        }

        // 3. Check attribute on type
        var attribute = aggregateType.GetCustomAttribute<SnapshotPolicyAttribute>();
        if (attribute is not null)
        {
            return SnapshotPolicy.FromAttribute(attribute);
        }

        // 4. Use default policy if set
        return _options.DefaultPolicy;
    }
}
