using ErikLieben.FA.ES.AspNetCore.MinimalApis.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    public class AddEventStoreMinimalApis
    {
        [Fact]
        public void Should_return_same_service_collection()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.AddEventStoreMinimalApis();

            // Assert
            Assert.Same(services, result);
        }

        [Fact]
        public void Should_allow_chaining_with_other_service_registrations()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services
                .AddEventStoreMinimalApis()
                .AddLogging();

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void Should_be_idempotent()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act - call multiple times
            services.AddEventStoreMinimalApis();
            services.AddEventStoreMinimalApis();
            var result = services.AddEventStoreMinimalApis();

            // Assert - should not throw and return same collection
            Assert.Same(services, result);
        }

        [Fact]
        public void Should_not_add_any_services_currently()
        {
            // Arrange
            var services = new ServiceCollection();
            var initialCount = services.Count;

            // Act
            services.AddEventStoreMinimalApis();

            // Assert - currently this is a no-op extension point
            Assert.Equal(initialCount, services.Count);
        }

        [Fact]
        public void Should_preserve_existing_services()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ITestService, TestService>();

            // Act
            services.AddEventStoreMinimalApis();

            // Assert
            var provider = services.BuildServiceProvider();
            var testService = provider.GetService<ITestService>();
            Assert.NotNull(testService);
        }

        private interface ITestService { }
        private class TestService : ITestService { }
    }
}
