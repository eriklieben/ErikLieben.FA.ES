using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Projections;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class WhenParameterValueFactoryAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new WhenParameterValueFactoryAttribute<TestFactory>();

            // Assert
            Assert.IsType<Attribute>(sut, exactMatch: false);
        }

        [Fact]
        public void Should_restrict_usage_to_methods_only()
        {
            // Arrange & Act
            var attributes = typeof(WhenParameterValueFactoryAttribute<TestFactory>)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

            // Assert
            Assert.NotEmpty(attributes);
            var usage = (AttributeUsageAttribute)attributes[0];
            Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        }

        [Fact]
        public void Should_allow_multiple_usages()
        {
            // Arrange & Act
            var attributes = typeof(WhenParameterValueFactoryAttribute<TestFactory>)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

            // Assert
            Assert.NotEmpty(attributes);
            var usage = (AttributeUsageAttribute)attributes[0];
            Assert.True(usage.AllowMultiple);
        }

        private class TestFactory : IProjectionWhenParameterValueFactory { }
    }
}