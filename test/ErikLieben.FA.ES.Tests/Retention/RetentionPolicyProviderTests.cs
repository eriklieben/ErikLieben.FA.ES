using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Retention;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Tests.Retention;

public class RetentionPolicyProviderTests
{
    [Fact]
    public void GetPolicy_ReturnsNullWhenNoPolicy()
    {
        var options = new RetentionOptions();
        var provider = new RetentionPolicyProvider(Options.Create(options));

        var policy = provider.GetPolicy<AggregateWithoutPolicy>();

        Assert.Null(policy);
    }

    [Fact]
    public void GetPolicy_ReturnsPolicyFromAttribute()
    {
        var options = new RetentionOptions();
        var provider = new RetentionPolicyProvider(Options.Create(options));

        var policy = provider.GetPolicy<AggregateWithPolicy>();

        Assert.NotNull(policy);
        Assert.Equal(TimeSpan.FromDays(365), policy.MaxAge);
        Assert.Equal(1000, policy.MaxEvents);
        Assert.Equal(RetentionAction.Migrate, policy.Action);
    }

    [Fact]
    public void GetPolicy_ReturnsRegisteredPolicyOverAttribute()
    {
        var options = new RetentionOptions();
        var provider = new RetentionPolicyProvider(Options.Create(options));
        var registeredPolicy = new RetentionPolicy { MaxEvents = 500 };

        provider.RegisterPolicy(typeof(AggregateWithPolicy), registeredPolicy);
        var policy = provider.GetPolicy<AggregateWithPolicy>();

        Assert.NotNull(policy);
        Assert.Equal(500, policy.MaxEvents);
    }

    [Fact]
    public void GetPolicy_ReturnsConfigOverrideOverAttribute()
    {
        var overridePolicy = new RetentionPolicy { MaxEvents = 2000 };
        var options = new RetentionOptions
        {
            PolicyOverrides = { ["AggregateWithPolicy"] = overridePolicy }
        };
        var provider = new RetentionPolicyProvider(Options.Create(options));

        var policy = provider.GetPolicy(typeof(AggregateWithPolicy));

        Assert.NotNull(policy);
        Assert.Equal(2000, policy.MaxEvents);
    }

    [Fact]
    public void GetPolicy_ReturnsDefaultPolicyWhenNoOther()
    {
        var defaultPolicy = new RetentionPolicy { MaxEvents = 5000 };
        var options = new RetentionOptions { DefaultPolicy = defaultPolicy };
        var provider = new RetentionPolicyProvider(Options.Create(options));

        var policy = provider.GetPolicy<AggregateWithoutPolicy>();

        Assert.NotNull(policy);
        Assert.Equal(5000, policy.MaxEvents);
    }

    [Fact]
    public void GetPolicy_CachesResult()
    {
        var options = new RetentionOptions();
        var provider = new RetentionPolicyProvider(Options.Create(options));

        var policy1 = provider.GetPolicy<AggregateWithPolicy>();
        var policy2 = provider.GetPolicy<AggregateWithPolicy>();

        Assert.Same(policy1, policy2);
    }

    [Fact]
    public void RegisterPolicy_InvalidatesCacheForType()
    {
        var options = new RetentionOptions();
        var provider = new RetentionPolicyProvider(Options.Create(options));

        var policy1 = provider.GetPolicy<AggregateWithPolicy>();
        provider.RegisterPolicy(typeof(AggregateWithPolicy), new RetentionPolicy { MaxEvents = 999 });
        var policy2 = provider.GetPolicy<AggregateWithPolicy>();

        Assert.NotSame(policy1, policy2);
        Assert.Equal(999, policy2!.MaxEvents);
    }

    [Fact]
    public void GetRegisteredTypes_ReturnsRegisteredAndOverrideTypes()
    {
        var options = new RetentionOptions
        {
            PolicyOverrides = { ["Order"] = new RetentionPolicy() }
        };
        var provider = new RetentionPolicyProvider(Options.Create(options));
        provider.RegisterPolicy(typeof(AggregateWithPolicy), new RetentionPolicy());

        var types = provider.GetRegisteredTypes().ToList();

        Assert.Contains("AggregateWithPolicy", types);
        Assert.Contains("Order", types);
    }

    private class AggregateWithoutPolicy { }

    [RetentionPolicy(MaxAge = "365d", MaxEvents = 1000, Action = RetentionAction.Migrate)]
    private class AggregateWithPolicy { }
}
