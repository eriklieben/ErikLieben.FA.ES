using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Attributes;
using System;
using ErikLieben.FA.ES.Documents;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class PreAppendStreamActionAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new PreAppendStreamActionAttribute<TestAction>();

            // Assert
            Assert.IsAssignableFrom<Attribute>(sut);
        }

        [Fact]
        public void Should_constrain_generic_parameter_to_ipreappendaction()
        {
            // Arrange & Act
            var genericArguments = typeof(PreAppendStreamActionAttribute<>).GetGenericArguments();

            // Assert
            Assert.Single(genericArguments);
            var constraints = genericArguments[0].GetGenericParameterConstraints();
            Assert.Contains(typeof(IPreAppendAction), constraints);
        }

        private class TestAction : IPreAppendAction
        {
            public Func<T> PreAppend<T>(T data, JsonEvent @event, IObjectDocument objectDocument) where T : class
            {
                throw new NotImplementedException();
            }
        }
    }
}
