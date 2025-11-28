using System;
using System.Linq;
using ErikLieben.FA.ES.Attributes;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes;

public class WhenAttributeTests
{
    private class TestEvent
    {
    }

    [Fact]
    public void Should_create_instance()
    {
        // Arrange & Act
        var attribute = new WhenAttribute<TestEvent>();

        // Assert
        Assert.NotNull(attribute);
    }

    [Fact]
    public void Should_be_applicable_only_to_methods()
    {
        // Arrange
        var attributeUsage = typeof(WhenAttribute<>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.Equal(AttributeTargets.Method, attributeUsage.ValidOn);
        Assert.False(attributeUsage.Inherited);
        Assert.False(attributeUsage.AllowMultiple);
    }
}
