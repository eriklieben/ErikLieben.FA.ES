using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Processors;
using NSubstitute;
using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Aggregates
{
    public class AggregateFactoryTests
    {
        // Implementation of abstract class for testing
        private class TestAggregateFactory : AggregateFactory
        {
            public TestAggregateFactory(IServiceProvider serviceProvider) : base(serviceProvider)
            {
            }

            protected override Type InternalGet(Type type)
            {
                return typeof(IAggregateFactory<>).MakeGenericType(type);
            }
        }

        [Fact]
        public void Should_throw_when_service_provider_is_null()
        {
            // Arrange
            IServiceProvider serviceProvider = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TestAggregateFactory(serviceProvider));
        }

        [Fact]
        public void Should_return_factory_from_service_provider()
        {
            // Arrange
            var serviceProvider = Substitute.For<IServiceProvider>();
            var factory = Substitute.For<IAggregateFactory<IBase>>();

            serviceProvider.GetService(typeof(IAggregateFactory<IBase>)).Returns(factory);

            var sut = new TestAggregateFactory(serviceProvider);

            // Act
            var result = sut.GetFactory<IBase>();

            // Assert
            Assert.Same(factory, result);
            serviceProvider.Received(1).GetService(typeof(IAggregateFactory<IBase>));
        }

        [Fact]
        public void Should_return_null_when_factory_not_found()
        {
            // Arrange
            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(typeof(IAggregateFactory<IBase>)).Returns(null);

            var sut = new TestAggregateFactory(serviceProvider);

            // Act
            var result = sut.GetFactory<IBase>();

            // Assert
            Assert.Null(result);
            serviceProvider.Received(1).GetService(typeof(IAggregateFactory<IBase>));
        }

        [Fact]
        public void Should_return_null_when_service_is_not_of_expected_type()
        {
            // Arrange
            var serviceProvider = Substitute.For<IServiceProvider>();
            // Return an object that is not an IAggregateFactory<IBase>
            serviceProvider.GetService(typeof(IAggregateFactory<IBase>)).Returns(new object());

            var sut = new TestAggregateFactory(serviceProvider);

            // Act
            var result = sut.GetFactory<IBase>();

            // Assert
            Assert.Null(result);
            serviceProvider.Received(1).GetService(typeof(IAggregateFactory<IBase>));
        }

        [Fact]
        public void Should_return_factory_by_type()
        {
            // Arrange
            var serviceProvider = Substitute.For<IServiceProvider>();
            var factory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            var baseType = typeof(IBase);

            serviceProvider.GetService(typeof(IAggregateFactory<>).MakeGenericType(baseType)).Returns(factory);

            var sut = new TestAggregateFactory(serviceProvider);

            // Act
            var result = sut.GetFactory(baseType);

            // Assert
            Assert.Same(factory, result);
            serviceProvider.Received(1).GetService(typeof(IAggregateFactory<>).MakeGenericType(baseType));
        }

        [Fact]
        public void Should_return_null_when_factory_by_type_not_found()
        {
            // Arrange
            var serviceProvider = Substitute.For<IServiceProvider>();
            var baseType = typeof(IBase);

            serviceProvider.GetService(typeof(IAggregateFactory<>).MakeGenericType(baseType)).Returns(null);

            var sut = new TestAggregateFactory(serviceProvider);

            // Act
            var result = sut.GetFactory(baseType);

            // Assert
            Assert.Null(result);
            serviceProvider.Received(1).GetService(typeof(IAggregateFactory<>).MakeGenericType(baseType));
        }

        [Fact]
        public void Should_return_null_when_service_by_type_is_not_of_expected_type()
        {
            // Arrange
            var serviceProvider = Substitute.For<IServiceProvider>();
            var baseType = typeof(IBase);

            // Return an object that is not an IAggregateCovarianceFactory<IBase>
            serviceProvider.GetService(typeof(IAggregateFactory<>).MakeGenericType(baseType)).Returns(new object());

            var sut = new TestAggregateFactory(serviceProvider);

            // Act
            var result = sut.GetFactory(baseType);

            // Assert
            Assert.Null(result);
            serviceProvider.Received(1).GetService(typeof(IAggregateFactory<>).MakeGenericType(baseType));
        }
    }
}
