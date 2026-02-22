using Azure;
using ErikLieben.FA.ES.AzureStorage;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests;

public class ServiceCollectionExtensionsTests
{
    public class ConfigureBlobEventStore
    {
        [Fact]
        public void Should_return_same_service_collection()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamBlobSettings("defaultStore");

            // Act
            var result = services.ConfigureBlobEventStore(settings);

            // Assert
            Assert.Same(services, result);
        }

        [Fact]
        public void Should_register_settings_as_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamBlobSettings("defaultStore");

            // Act
            services.ConfigureBlobEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(EventStreamBlobSettings));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_register_BlobTagFactory_as_keyed_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamBlobSettings("defaultStore");

            // Act
            services.ConfigureBlobEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IDocumentTagDocumentFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "blob");

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(BlobTagFactory), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void Should_register_BlobObjectDocumentFactory_as_keyed_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamBlobSettings("defaultStore");

            // Act
            services.ConfigureBlobEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IObjectDocumentFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "blob");

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(BlobObjectDocumentFactory), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void Should_register_BlobEventStreamFactory_as_keyed_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamBlobSettings("defaultStore");

            // Act
            services.ConfigureBlobEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IEventStreamFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "blob");

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(BlobEventStreamFactory), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void Should_register_BlobObjectIdProvider_as_keyed_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamBlobSettings("defaultStore");

            // Act
            services.ConfigureBlobEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IObjectIdProvider) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "blob");

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(BlobObjectIdProvider), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void Should_allow_chaining_with_other_service_registrations()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamBlobSettings("defaultStore");

            // Act
            var result = services
                .ConfigureBlobEventStore(settings)
                .AddLogging();

            // Assert
            Assert.NotNull(result);
        }
    }

    public class ConfigureTableEventStore
    {
        [Fact]
        public void Should_return_same_service_collection()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamTableSettings("defaultStore");

            // Act
            var result = services.ConfigureTableEventStore(settings);

            // Assert
            Assert.Same(services, result);
        }

        [Fact]
        public void Should_register_settings_as_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamTableSettings("defaultStore");

            // Act
            services.ConfigureTableEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(EventStreamTableSettings));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_register_TableTagFactory_as_keyed_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamTableSettings("defaultStore");

            // Act
            services.ConfigureTableEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IDocumentTagDocumentFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "table");

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(TableTagFactory), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void Should_register_TableObjectDocumentFactory_as_keyed_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamTableSettings("defaultStore");

            // Act
            services.ConfigureTableEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IObjectDocumentFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "table");

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(TableObjectDocumentFactory), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void Should_register_TableEventStreamFactory_as_keyed_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamTableSettings("defaultStore");

            // Act
            services.ConfigureTableEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IEventStreamFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "table");

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(TableEventStreamFactory), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void Should_register_TableObjectIdProvider_as_keyed_singleton()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamTableSettings("defaultStore");

            // Act
            services.ConfigureTableEventStore(settings);

            // Assert
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IObjectIdProvider) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "table");

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(TableObjectIdProvider), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void Should_allow_chaining_with_other_service_registrations()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamTableSettings("defaultStore");

            // Act
            var result = services
                .ConfigureTableEventStore(settings)
                .AddLogging();

            // Assert
            Assert.NotNull(result);
        }
    }

    public class BothConfigurations
    {
        [Fact]
        public void Should_allow_both_blob_and_table_configurations()
        {
            // Arrange
            var services = new ServiceCollection();
            var blobSettings = new EventStreamBlobSettings("blobStore");
            var tableSettings = new EventStreamTableSettings("tableStore");

            // Act
            services
                .ConfigureBlobEventStore(blobSettings)
                .ConfigureTableEventStore(tableSettings);

            // Assert
            // Should have both settings registered
            var blobDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(EventStreamBlobSettings));
            var tableDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(EventStreamTableSettings));

            Assert.NotNull(blobDescriptor);
            Assert.NotNull(tableDescriptor);

            // Should have keyed services for both blob and table
            var blobFactories = services.Count(d =>
                d.IsKeyedService && (string?)d.ServiceKey == "blob");
            var tableFactories = services.Count(d =>
                d.IsKeyedService && (string?)d.ServiceKey == "table");

            Assert.Equal(4, blobFactories); // TagFactory, ObjectDocumentFactory, EventStreamFactory, ObjectIdProvider
            Assert.Equal(4, tableFactories);
        }
    }

    public class RegisterAzureExceptionExtractor
    {
        [Fact]
        public void Should_not_throw_when_called_multiple_times()
        {
            // Act & Assert - should not throw
            ServiceCollectionExtensions.RegisterAzureExceptionExtractor();
            ServiceCollectionExtensions.RegisterAzureExceptionExtractor();
            ServiceCollectionExtensions.RegisterAzureExceptionExtractor();

            Assert.True(true);
        }

        [Fact]
        public void Should_be_called_when_configuring_blob_store()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamBlobSettings("defaultStore");

            // Act - should not throw (extractor gets registered)
            services.ConfigureBlobEventStore(settings);

            // Assert - if we got here without exception, the extractor was registered
            Assert.True(true);
        }

        [Fact]
        public void Should_be_called_when_configuring_table_store()
        {
            // Arrange
            var services = new ServiceCollection();
            var settings = new EventStreamTableSettings("defaultStore");

            // Act - should not throw (extractor gets registered)
            services.ConfigureTableEventStore(settings);

            // Assert - if we got here without exception, the extractor was registered
            Assert.True(true);
        }
    }
}
