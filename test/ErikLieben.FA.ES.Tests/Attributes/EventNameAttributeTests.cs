using ErikLieben.FA.ES.Attributes;
using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class EventNameAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new EventNameAttribute("TestEvent");

            // Assert
            Assert.IsType<Attribute>(sut, exactMatch: false);
        }

        [Fact]
        public void Should_store_name_in_property()
        {
            // Arrange
            const string expectedName = "TestEvent";

            // Act
            var sut = new EventNameAttribute(expectedName);

            // Assert
            Assert.Equal(expectedName, sut.Name);
        }

        [Fact]
        public void Should_throw_when_name_is_null()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EventNameAttribute(null!));
        }

        [Fact]
        public void Should_restrict_usage_to_class_or_struct()
        {
            // Arrange & Act
            var attributes = typeof(EventNameAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

            // Assert
            Assert.NotEmpty(attributes);
            var usage = (AttributeUsageAttribute)attributes[0];
            Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        }
    }
}