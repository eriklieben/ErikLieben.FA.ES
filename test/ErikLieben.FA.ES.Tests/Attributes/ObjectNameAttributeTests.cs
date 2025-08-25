using ErikLieben.FA.ES.Attributes;
using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class ObjectNameAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new ObjectNameAttribute("TestName");

            // Assert
            Assert.IsAssignableFrom<Attribute>(sut);
        }

        [Fact]
        public void Should_store_name_in_property()
        {
            // Arrange
            const string expectedName = "TestName";

            // Act
            var sut = new ObjectNameAttribute(expectedName);

            // Assert
            Assert.Equal(expectedName, sut.Name);
        }

        [Fact]
        public void Should_throw_when_name_is_null()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ObjectNameAttribute(null!));
        }

        [Fact]
        public void Should_restrict_usage_to_class_or_struct()
        {
            // Arrange & Act
            var attributes = typeof(ObjectNameAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

            // Assert
            Assert.NotEmpty(attributes);
            var usage = (AttributeUsageAttribute)attributes[0];
            Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        }
    }
}