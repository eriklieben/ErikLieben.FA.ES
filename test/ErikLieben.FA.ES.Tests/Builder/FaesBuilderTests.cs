using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ErikLieben.FA.ES.Tests.Builder;

public class FaesBuilderTests
{
    public class AddFaes
    {
        [Fact]
        public void Should_register_default_settings_with_blob_storage_type()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddFaes(_ => { });
            var provider = services.BuildServiceProvider();

            // Assert
            var settings = provider.GetRequiredService<EventStreamDefaultTypeSettings>();
            Assert.Equal("blob", settings.StreamType);
            Assert.Equal("blob", settings.DocumentType);
        }

        [Fact]
        public void Should_register_custom_storage_type()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddFaes(builder => builder.UseDefaultStorage("table"));
            var provider = services.BuildServiceProvider();

            // Assert
            var settings = provider.GetRequiredService<EventStreamDefaultTypeSettings>();
            Assert.Equal("table", settings.StreamType);
        }

        [Fact]
        public void Should_register_core_factories()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddFaes(_ => { });
            var provider = services.BuildServiceProvider();

            // Assert
            Assert.NotNull(provider.GetService<IObjectDocumentFactory>());
            Assert.NotNull(provider.GetService<IDocumentTagDocumentFactory>());
            Assert.NotNull(provider.GetService<IEventStreamFactory>());
            Assert.NotNull(provider.GetService<IObjectIdProvider>());
        }

        [Fact]
        public void Should_register_keyed_dictionaries()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddFaes(_ => { });
            var provider = services.BuildServiceProvider();

            // Assert - dictionaries should be registered even if empty
            var objectDocFactoryDict = provider.GetService<IDictionary<string, IObjectDocumentFactory>>();
            var tagFactoryDict = provider.GetService<IDictionary<string, IDocumentTagDocumentFactory>>();
            var streamFactoryDict = provider.GetService<IDictionary<string, IEventStreamFactory>>();
            var objectIdDict = provider.GetService<IDictionary<string, IObjectIdProvider>>();

            Assert.NotNull(objectDocFactoryDict);
            Assert.NotNull(tagFactoryDict);
            Assert.NotNull(streamFactoryDict);
            Assert.NotNull(objectIdDict);
        }

        [Fact]
        public void Should_allow_chaining()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act - should compile and not throw
            var result = services.AddFaes(builder => builder
                .UseDefaultStorage("cosmosdb")
                .UseDefaultStorage("table", "table"));

            // Assert
            Assert.Same(services, result);
        }

        [Fact]
        public void Should_throw_when_services_null()
        {
            // Arrange
            IServiceCollection? services = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services!.AddFaes(_ => { }));
        }

        [Fact]
        public void Should_throw_when_configure_null()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => services.AddFaes(null!));
        }

        [Fact]
        public void AddFaes_without_configure_should_use_defaults()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddFaes();
            var provider = services.BuildServiceProvider();

            // Assert
            var settings = provider.GetRequiredService<EventStreamDefaultTypeSettings>();
            Assert.Equal("blob", settings.StreamType);
        }
    }

    public class UseDefaultStorage
    {
        [Fact]
        public void Should_throw_when_storage_type_null()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = new FaesBuilder(services);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.UseDefaultStorage(null!));
        }

        [Fact]
        public void Should_throw_when_document_type_null()
        {
            // Arrange
            var services = new ServiceCollection();
            var builder = new FaesBuilder(services);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.UseDefaultStorage("blob", null!));
        }
    }
}
