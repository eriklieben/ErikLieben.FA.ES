using System;
using System.Linq;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Upcasting;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes;

public class UseUpcasterAttributeTests
{
    // Test upcaster implementation
    private class TestUpcaster : IUpcastEvent
    {
        public bool CanUpcast(IEvent @event) => true;
        public IEnumerable<IEvent> UpCast(IEvent @event) => [@event];
    }

    private class AnotherUpcaster : IUpcastEvent
    {
        public bool CanUpcast(IEvent @event) => true;
        public IEnumerable<IEvent> UpCast(IEvent @event) => [@event];
    }

    [Fact]
    public void Should_create_instance_with_generic_type_parameter()
    {
        // Arrange & Act
        var attribute = new UseUpcasterAttribute<TestUpcaster>();

        // Assert
        Assert.Equal(typeof(TestUpcaster), attribute.UpcasterType);
    }

    [Fact]
    public void Should_be_applicable_to_classes()
    {
        // Arrange
        var attributeUsage = typeof(UseUpcasterAttribute<TestUpcaster>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
    }

    [Fact]
    public void Should_allow_multiple_attributes()
    {
        // Arrange
        var attributeUsage = typeof(UseUpcasterAttribute<TestUpcaster>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.True(attributeUsage.AllowMultiple);
    }

    [Fact]
    public void Should_not_be_inherited()
    {
        // Arrange
        var attributeUsage = typeof(UseUpcasterAttribute<TestUpcaster>)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.False(attributeUsage.Inherited);
    }

    [Fact]
    public void Should_work_with_multiple_different_upcasters()
    {
        // Arrange & Act
        var attr1 = new UseUpcasterAttribute<TestUpcaster>();
        var attr2 = new UseUpcasterAttribute<AnotherUpcaster>();

        // Assert
        Assert.Equal(typeof(TestUpcaster), attr1.UpcasterType);
        Assert.Equal(typeof(AnotherUpcaster), attr2.UpcasterType);
        Assert.NotEqual(attr1.UpcasterType, attr2.UpcasterType);
    }
}
