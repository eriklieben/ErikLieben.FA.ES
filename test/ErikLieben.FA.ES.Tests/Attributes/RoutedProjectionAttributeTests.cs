using System;
using System.Linq;
using ErikLieben.FA.ES.Attributes;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes;

public class RoutedProjectionAttributeTests
{
    [Fact]
    public void Should_store_router_type()
    {
        // Arrange & Act
        var attribute = new RoutedProjectionAttribute(typeof(string));

        // Assert
        Assert.Equal(typeof(string), attribute.RouterType);
    }

    [Fact]
    public void Should_be_applicable_only_to_classes()
    {
        // Arrange
        var attributeUsage = typeof(RoutedProjectionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
        Assert.False(attributeUsage.Inherited);
        Assert.False(attributeUsage.AllowMultiple);
    }
}
