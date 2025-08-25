using ErikLieben.FA.ES.Attributes;
using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class AggregateAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new AggregateAttribute();

            // Assert
            Assert.IsAssignableFrom<Attribute>(sut);
        }

        [Fact]
        public void Should_restrict_usage_to_class_or_struct()
        {
            // Arrange & Act
            var attributes = typeof(AggregateAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

            // Assert
            Assert.NotEmpty(attributes);
            var usage = (AttributeUsageAttribute)attributes[0];
            Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        }
    }
}