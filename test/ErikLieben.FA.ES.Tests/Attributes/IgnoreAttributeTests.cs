using ErikLieben.FA.ES.Attributes;
using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class IgnoreAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new IgnoreAttribute("Test reason");

            // Assert
            Assert.IsType<Attribute>(sut, exactMatch: false);
        }

        [Fact]
        public void Should_store_reason_in_property()
        {
            // Arrange
            const string expectedReason = "Test reason";

            // Act
            var sut = new IgnoreAttribute(expectedReason);

            // Assert
            Assert.Equal(expectedReason, sut.Reason);
        }

        [Fact]
        public void Should_restrict_usage_to_class_or_struct()
        {
            // Arrange & Act
            var attributes = typeof(IgnoreAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

            // Assert
            Assert.NotEmpty(attributes);
            var usage = (AttributeUsageAttribute)attributes[0];
            Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        }
    }
}