using System.Collections.Concurrent;
using System.Reflection;
using ErikLieben.FA.ES.Attributes;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Retention;

/// <summary>
/// Default implementation of <see cref="IRetentionPolicyProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// Policy resolution order:
/// 1. Explicitly registered policies via <see cref="RegisterPolicy"/>
/// 2. Configuration overrides from <see cref="RetentionOptions.PolicyOverrides"/>
/// 3. <see cref="RetentionPolicyAttribute"/> on the aggregate type
/// 4. <see cref="RetentionOptions.DefaultPolicy"/> if set
/// 5. null (no retention policy)
/// </para>
/// </remarks>
public class RetentionPolicyProvider : IRetentionPolicyProvider
{
    private readonly ConcurrentDictionary<Type, RetentionPolicy?> _policyCache = new();
    private readonly ConcurrentDictionary<Type, RetentionPolicy> _registeredPolicies = new();
    private readonly RetentionOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetentionPolicyProvider"/> class.
    /// </summary>
    /// <param name="options">The retention options.</param>
    public RetentionPolicyProvider(IOptions<RetentionOptions> options)
    {
        _options = options?.Value ?? RetentionOptions.Default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetentionPolicyProvider"/> class
    /// with the specified options directly.
    /// </summary>
    /// <param name="options">The retention options.</param>
    public RetentionPolicyProvider(RetentionOptions options)
    {
        _options = options ?? RetentionOptions.Default;
    }

    /// <inheritdoc />
    public RetentionPolicy? GetPolicy(Type aggregateType)
    {
        ArgumentNullException.ThrowIfNull(aggregateType);
        return _policyCache.GetOrAdd(aggregateType, ResolvePolicy);
    }

    /// <inheritdoc />
    public RetentionPolicy? GetPolicy<T>() where T : class
        => GetPolicy(typeof(T));

    /// <inheritdoc />
    public void RegisterPolicy(Type aggregateType, RetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(aggregateType);
        ArgumentNullException.ThrowIfNull(policy);

        _registeredPolicies[aggregateType] = policy;
        _policyCache.TryRemove(aggregateType, out _);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetRegisteredTypes()
    {
        var types = new HashSet<string>();

        // Add explicitly registered types
        foreach (var type in _registeredPolicies.Keys)
        {
            types.Add(type.Name);
        }

        // Add types from options overrides
        foreach (var typeName in _options.PolicyOverrides.Keys)
        {
            types.Add(typeName);
        }

        return types;
    }

    private RetentionPolicy? ResolvePolicy(Type aggregateType)
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
        var attribute = aggregateType.GetCustomAttribute<RetentionPolicyAttribute>();
        if (attribute is not null)
        {
            return RetentionPolicy.FromAttribute(attribute);
        }

        // 4. Use default policy if set
        return _options.DefaultPolicy;
    }
}
