using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class ServiceCollectionExtensionsTests
{
    public class ConfigureCosmosDbEventStore
    {
        [Fact]
        public void Should_register_settings_as_singleton()
        {
            var services = new ServiceCollection();
            var settings = new EventStreamCosmosDbSettings();

            services.ConfigureCosmosDbEventStore(settings);

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(EventStreamCosmosDbSettings));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_register_tag_factory_as_keyed_singleton()
        {
            var services = new ServiceCollection();
            var settings = new EventStreamCosmosDbSettings();

            services.ConfigureCosmosDbEventStore(settings);

            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IDocumentTagDocumentFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "cosmosdb");
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_register_object_document_factory_as_keyed_singleton()
        {
            var services = new ServiceCollection();
            var settings = new EventStreamCosmosDbSettings();

            services.ConfigureCosmosDbEventStore(settings);

            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IObjectDocumentFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "cosmosdb");
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_register_event_stream_factory_as_keyed_singleton()
        {
            var services = new ServiceCollection();
            var settings = new EventStreamCosmosDbSettings();

            services.ConfigureCosmosDbEventStore(settings);

            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IEventStreamFactory) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "cosmosdb");
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_register_object_id_provider_as_keyed_singleton()
        {
            var services = new ServiceCollection();
            var settings = new EventStreamCosmosDbSettings();

            services.ConfigureCosmosDbEventStore(settings);

            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IObjectIdProvider) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "cosmosdb");
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_return_same_service_collection()
        {
            var services = new ServiceCollection();
            var settings = new EventStreamCosmosDbSettings();

            var result = services.ConfigureCosmosDbEventStore(settings);

            Assert.Same(services, result);
        }
    }
}
