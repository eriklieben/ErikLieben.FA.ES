using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.CosmosDb.Builder;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Builder;

public class FaesBuilderExtensionsTests
{
    public class UseCosmosDbWithSettings
    {
        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FaesBuilderExtensions.UseCosmosDb(null!, new EventStreamCosmosDbSettings()));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(new ServiceCollection());

            Assert.Throws<ArgumentNullException>(() =>
                builder.UseCosmosDb((EventStreamCosmosDbSettings)null!));
        }

        [Fact]
        public void Should_register_settings_as_singleton()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.UseCosmosDb(new EventStreamCosmosDbSettings());

            Assert.Contains(services, s => s.ServiceType == typeof(EventStreamCosmosDbSettings));
        }

        [Fact]
        public void Should_register_tag_factory_as_keyed_singleton()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.UseCosmosDb(new EventStreamCosmosDbSettings());

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
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.UseCosmosDb(new EventStreamCosmosDbSettings());

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
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.UseCosmosDb(new EventStreamCosmosDbSettings());

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
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.UseCosmosDb(new EventStreamCosmosDbSettings());

            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IObjectIdProvider) &&
                d.IsKeyedService &&
                (string?)d.ServiceKey == "cosmosdb");
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_return_builder_for_chaining()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            var result = builder.UseCosmosDb(new EventStreamCosmosDbSettings());

            Assert.Same(builder, result);
        }
    }

    public class UseCosmosDbWithAction
    {
        [Fact]
        public void Should_throw_when_configure_action_is_null()
        {
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(new ServiceCollection());

            Assert.Throws<ArgumentNullException>(() =>
                builder.UseCosmosDb((Action<EventStreamCosmosDbSettings>)null!));
        }

        [Fact]
        public void Should_invoke_configure_action_and_register_services()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);
            var configured = false;

            builder.UseCosmosDb(settings =>
            {
                configured = true;
                settings = settings with { DatabaseName = "custom-db" };
            });

            Assert.True(configured);
            Assert.Contains(services, s => s.ServiceType == typeof(EventStreamCosmosDbSettings));
        }
    }

    public class WithCosmosDbHealthCheckMethod
    {
        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FaesBuilderExtensions.WithCosmosDbHealthCheck(null!));
        }

        [Fact]
        public void Should_return_builder_for_chaining()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            var result = builder.WithCosmosDbHealthCheck();

            Assert.Same(builder, result);
        }

        [Fact]
        public void Should_register_health_check()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.WithCosmosDbHealthCheck();

            // Health check registration adds services to the service collection
            Assert.True(services.Count > 0);
        }
    }

    public class WithCosmosDbProjectionStatusCoordinatorMethod
    {
        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FaesBuilderExtensions.WithCosmosDbProjectionStatusCoordinator(null!, "test-db"));
        }

        [Fact]
        public void Should_throw_when_database_name_is_null()
        {
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(new ServiceCollection());

            Assert.Throws<ArgumentNullException>(() =>
                builder.WithCosmosDbProjectionStatusCoordinator(null!));
        }

        [Fact]
        public void Should_register_projection_status_coordinator()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.WithCosmosDbProjectionStatusCoordinator("test-db");

            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IProjectionStatusCoordinator));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void Should_return_builder_for_chaining()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            var result = builder.WithCosmosDbProjectionStatusCoordinator("test-db");

            Assert.Same(builder, result);
        }

        [Fact]
        public void Should_accept_custom_container_name()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            var result = builder.WithCosmosDbProjectionStatusCoordinator("test-db", "custom-container");

            Assert.Same(builder, result);
            Assert.Contains(services, d => d.ServiceType == typeof(IProjectionStatusCoordinator));
        }
    }
}
