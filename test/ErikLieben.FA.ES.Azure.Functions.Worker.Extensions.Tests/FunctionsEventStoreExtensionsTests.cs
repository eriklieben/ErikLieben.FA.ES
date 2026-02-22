using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class FunctionsEventStoreExtensionsTests
{
    public class ConfigureEventStoreBindingsMethod : FunctionsEventStoreExtensionsTests
    {
        [Fact]
        public void Should_register_EventStreamConverter()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ConfigureEventStoreBindings();

            // Assert
            Assert.Contains(services, sd => sd.ImplementationType?.Name == "EventStreamConverter");
        }

        [Fact]
        public void Should_register_ProjectionConverter()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ConfigureEventStoreBindings();

            // Assert
            Assert.Contains(services, sd => sd.ImplementationType?.Name == "ProjectionConverter");
        }

        [Fact]
        public void Should_register_converters_as_IInputConverter()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ConfigureEventStoreBindings();

            // Assert
            Assert.Contains(services, sd =>
                sd.ServiceType == typeof(IInputConverter) &&
                sd.ImplementationType?.Name == "EventStreamConverter");
            Assert.Contains(services, sd =>
                sd.ServiceType == typeof(IInputConverter) &&
                sd.ImplementationType?.Name == "ProjectionConverter");
        }

        [Fact]
        public void Should_register_converters_as_singletons()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ConfigureEventStoreBindings();

            // Assert
            Assert.Contains(services, sd =>
                sd.ImplementationType?.Name == "EventStreamConverter" &&
                sd.Lifetime == ServiceLifetime.Singleton);
            Assert.Contains(services, sd =>
                sd.ImplementationType?.Name == "ProjectionConverter" &&
                sd.Lifetime == ServiceLifetime.Singleton);
        }

        [Fact]
        public void Should_return_service_collection_for_chaining()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.ConfigureEventStoreBindings();

            // Assert
            Assert.Same(services, result);
        }

        [Fact]
        public void Should_allow_multiple_calls_without_error()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ConfigureEventStoreBindings();
            services.ConfigureEventStoreBindings();

            // Assert
            var converterCount = services.Count(sd => sd.ServiceType == typeof(IInputConverter));
            Assert.Equal(4, converterCount); // 2 converters x 2 calls
        }
    }
}
