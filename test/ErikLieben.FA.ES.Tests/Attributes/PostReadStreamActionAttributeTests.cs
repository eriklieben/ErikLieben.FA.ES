using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Attributes;
using System;
using ErikLieben.FA.ES.Documents;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes
{
    public class PostReadStreamActionAttributeTests
    {
        [Fact]
        public void Should_implement_attribute()
        {
            // Arrange & Act
            var sut = new PostReadStreamActionAttribute<TestAction>();

            // Assert
            Assert.IsType<Attribute>(sut, exactMatch: false);
        }

        [Fact]
        public void Should_constrain_generic_parameter_to_ipostreadaction()
        {
            // Arrange & Act
            var genericArguments = typeof(PostReadStreamActionAttribute<>).GetGenericArguments();

            // Assert
            Assert.Single(genericArguments);
            var constraints = genericArguments[0].GetGenericParameterConstraints();
            Assert.Contains(typeof(IPostReadAction), constraints);
        }

        private class TestAction : IPostReadAction
        {
            public Func<T> PostRead<T>(T data, IEvent @event, IObjectDocument objectDocument) where T : class
            {
                throw new NotImplementedException();
            }
        }
    }
}
