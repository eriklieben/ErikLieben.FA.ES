using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Snapshots;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Tests.Snapshots;

public class SnapshotPolicyProviderTests
{
    [Fact]
    public void GetPolicy_ReturnsNullWhenNoPolicy()
    {
        var options = new SnapshotOptions();
        var provider = new SnapshotPolicyProvider(Options.Create(options));

        var policy = provider.GetPolicy<AggregateWithoutPolicy>();

        Assert.Null(policy);
    }

    [Fact]
    public void GetPolicy_ReturnsPolicyFromAttribute()
    {
        var options = new SnapshotOptions();
        var provider = new SnapshotPolicyProvider(Options.Create(options));

        var policy = provider.GetPolicy<AggregateWithPolicy>();

        Assert.NotNull(policy);
        Assert.Equal(100, policy.Every);
        Assert.Equal(5, policy.KeepSnapshots);
    }

    [Fact]
    public void GetPolicy_ReturnsRegisteredPolicyOverAttribute()
    {
        var options = new SnapshotOptions();
        var provider = new SnapshotPolicyProvider(Options.Create(options));
        var registeredPolicy = new SnapshotPolicy { Every = 50 };

        provider.RegisterPolicy(typeof(AggregateWithPolicy), registeredPolicy);
        var policy = provider.GetPolicy<AggregateWithPolicy>();

        Assert.NotNull(policy);
        Assert.Equal(50, policy.Every);
    }

    [Fact]
    public void GetPolicy_ReturnsConfigOverrideOverAttribute_ShortName()
    {
        var overridePolicy = new SnapshotPolicy { Every = 200 };
        var options = new SnapshotOptions
        {
            PolicyOverrides = { ["AggregateWithPolicy"] = overridePolicy }
        };
        var provider = new SnapshotPolicyProvider(Options.Create(options));

        // Note: Short name matching in PolicyOverrides
        var policy = provider.GetPolicy(typeof(AggregateWithPolicy));

        Assert.NotNull(policy);
        Assert.Equal(200, policy.Every);
    }

    [Fact]
    public void GetPolicy_ReturnsDefaultPolicyWhenNoOther()
    {
        var defaultPolicy = new SnapshotPolicy { Every = 500 };
        var options = new SnapshotOptions { DefaultPolicy = defaultPolicy };
        var provider = new SnapshotPolicyProvider(Options.Create(options));

        var policy = provider.GetPolicy<AggregateWithoutPolicy>();

        Assert.NotNull(policy);
        Assert.Equal(500, policy.Every);
    }

    [Fact]
    public void GetPolicy_CachesResult()
    {
        var options = new SnapshotOptions();
        var provider = new SnapshotPolicyProvider(Options.Create(options));

        var policy1 = provider.GetPolicy<AggregateWithPolicy>();
        var policy2 = provider.GetPolicy<AggregateWithPolicy>();

        Assert.Same(policy1, policy2);
    }

    [Fact]
    public void RegisterPolicy_InvalidatesCacheForType()
    {
        var options = new SnapshotOptions();
        var provider = new SnapshotPolicyProvider(Options.Create(options));

        var policy1 = provider.GetPolicy<AggregateWithPolicy>();
        provider.RegisterPolicy(typeof(AggregateWithPolicy), new SnapshotPolicy { Every = 999 });
        var policy2 = provider.GetPolicy<AggregateWithPolicy>();

        Assert.NotSame(policy1, policy2);
        Assert.Equal(999, policy2!.Every);
    }

    [Fact]
    public void GetPolicy_SupportsFullTypeName()
    {
        var overridePolicy = new SnapshotPolicy { Every = 300 };
        // Use the actual full type name which includes the enclosing class for nested types
        var fullTypeName = typeof(AggregateWithPolicy).FullName!;
        var options = new SnapshotOptions
        {
            PolicyOverrides =
            {
                [fullTypeName] = overridePolicy
            }
        };
        var provider = new SnapshotPolicyProvider(Options.Create(options));

        var policy = provider.GetPolicy<AggregateWithPolicy>();

        Assert.NotNull(policy);
        Assert.Equal(300, policy.Every);
    }

    private class AggregateWithoutPolicy { }

    [SnapshotPolicy(Every = 100, KeepSnapshots = 5)]
    private class AggregateWithPolicy { }
}
