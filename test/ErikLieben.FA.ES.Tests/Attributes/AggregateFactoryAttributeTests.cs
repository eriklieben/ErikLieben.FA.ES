using ErikLieben.FA.ES.Attributes;
using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class AggregateFactoryAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new AggregateFactoryAttribute();

            // Assert
            Assert.IsType<Attribute>(sut, exactMatch: false);
        }

        [Fact]
        public void Should_restrict_usage_to_class_or_struct()
        {
            // Arrange & Act
            var attributes = typeof(AggregateFactoryAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

            // Assert
            Assert.NotEmpty(attributes);
            var usage = (AttributeUsageAttribute)attributes[0];
            Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        }
    }
}