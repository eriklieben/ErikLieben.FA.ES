using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.Serialization;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Configuration;

public class CosmosClientOptionsFactoryTests
{
    public class CreateDefaultMethod
    {
        [Fact]
        public void Should_return_non_null_options()
        {
            var options = CosmosClientOptionsFactory.CreateDefault();

            Assert.NotNull(options);
        }

        [Fact]
        public void Should_use_direct_connection_mode()
        {
            var options = CosmosClientOptionsFactory.CreateDefault();

            Assert.Equal(ConnectionMode.Direct, options.ConnectionMode);
        }

        [Fact]
        public void Should_set_aot_compatible_serializer()
        {
            var options = CosmosClientOptionsFactory.CreateDefault();

            Assert.NotNull(options.Serializer);
            Assert.IsType<CosmosDbSystemTextJsonSerializer>(options.Serializer);
        }
    }

    public class CreateForDevelopmentMethod
    {
        [Fact]
        public void Should_return_non_null_options()
        {
            var options = CosmosClientOptionsFactory.CreateForDevelopment();

            Assert.NotNull(options);
        }

        [Fact]
        public void Should_use_gateway_connection_mode()
        {
            var options = CosmosClientOptionsFactory.CreateForDevelopment();

            Assert.Equal(ConnectionMode.Gateway, options.ConnectionMode);
        }

        [Fact]
        public void Should_set_aot_compatible_serializer()
        {
            var options = CosmosClientOptionsFactory.CreateForDevelopment();

            Assert.NotNull(options.Serializer);
            Assert.IsType<CosmosDbSystemTextJsonSerializer>(options.Serializer);
        }
    }

    public class CreateForDevelopmentWithHttpClientFactory
    {
        [Fact]
        public void Should_throw_when_http_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                CosmosClientOptionsFactory.CreateForDevelopment(null!));
        }

        [Fact]
        public void Should_use_gateway_connection_mode()
        {
            var options = CosmosClientOptionsFactory.CreateForDevelopment(() => new HttpClient());

            Assert.Equal(ConnectionMode.Gateway, options.ConnectionMode);
        }

        [Fact]
        public void Should_set_http_client_factory()
        {
            Func<HttpClient> factory = () => new HttpClient();

            var options = CosmosClientOptionsFactory.CreateForDevelopment(factory);

            Assert.NotNull(options.HttpClientFactory);
            Assert.Same(factory, options.HttpClientFactory);
        }

        [Fact]
        public void Should_set_aot_compatible_serializer()
        {
            var options = CosmosClientOptionsFactory.CreateForDevelopment(() => new HttpClient());

            Assert.NotNull(options.Serializer);
            Assert.IsType<CosmosDbSystemTextJsonSerializer>(options.Serializer);
        }
    }

    public class WithAotSerializerMethod
    {
        [Fact]
        public void Should_throw_when_options_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                CosmosClientOptionsFactory.WithAotSerializer(null!));
        }

        [Fact]
        public void Should_set_serializer_on_existing_options()
        {
            var options = new CosmosClientOptions();

            var result = options.WithAotSerializer();

            Assert.NotNull(result.Serializer);
            Assert.IsType<CosmosDbSystemTextJsonSerializer>(result.Serializer);
        }

        [Fact]
        public void Should_return_same_options_instance_for_chaining()
        {
            var options = new CosmosClientOptions();

            var result = options.WithAotSerializer();

            Assert.Same(options, result);
        }

        [Fact]
        public void Should_preserve_other_option_settings()
        {
            var options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway
            };

            var result = options.WithAotSerializer();

            Assert.Equal(ConnectionMode.Gateway, result.ConnectionMode);
        }
    }
}
