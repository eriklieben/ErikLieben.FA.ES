using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Attributes;
using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class StreamActionAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new StreamActionAttribute<TestAction>();

            // Assert
            Assert.IsAssignableFrom<Attribute>(sut);
        }

        [Fact]
        public void Should_constrain_generic_parameter_to_iaction()
        {
            // Arrange & Act
            var genericArguments = typeof(StreamActionAttribute<>).GetGenericArguments();

            // Assert
            Assert.Single(genericArguments);
            var constraints = genericArguments[0].GetGenericParameterConstraints();
            Assert.Contains(typeof(IAction), constraints);
        }

        private class TestAction : IAction { }
    }
}