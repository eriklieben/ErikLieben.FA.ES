using ErikLieben.FA.ES.S3.Builder;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests.Builder;

public class FaesBuilderExtensionsTests
{
    public class UseS3StorageWithSettings
    {
        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FaesBuilderExtensions.UseS3Storage(null!, new EventStreamS3Settings("s3")));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(new ServiceCollection());

            Assert.Throws<ArgumentNullException>(() =>
                builder.UseS3Storage((EventStreamS3Settings)null!));
        }

        [Fact]
        public void Should_register_s3_client_factory()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.UseS3Storage(new EventStreamS3Settings("s3"));

            Assert.Contains(services, s => s.ServiceType == typeof(IS3ClientFactory));
        }

        [Fact]
        public void Should_register_settings()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            builder.UseS3Storage(new EventStreamS3Settings("s3"));

            Assert.Contains(services, s => s.ServiceType == typeof(EventStreamS3Settings));
        }

        [Fact]
        public void Should_return_builder_for_chaining()
        {
            var services = new ServiceCollection();
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(services);

            var result = builder.UseS3Storage(new EventStreamS3Settings("s3"));

            Assert.Same(builder, result);
        }
    }

    public class UseS3StorageWithAction
    {
        [Fact]
        public void Should_throw_when_configure_is_null()
        {
            var builder = Substitute.For<IFaesBuilder>();
            builder.Services.Returns(new ServiceCollection());

            Assert.Throws<ArgumentNullException>(() =>
                builder.UseS3Storage((Action<EventStreamS3Settings>)null!));
        }
    }

    public class WithS3HealthCheck
    {
        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FaesBuilderExtensions.WithS3HealthCheck(null!));
        }
    }

    public class ConfigureS3EventStore
    {
        [Fact]
        public void Should_throw_when_services_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FaesBuilderExtensions.ConfigureS3EventStore(null!, new EventStreamS3Settings("s3")));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ServiceCollection().ConfigureS3EventStore(null!));
        }

        [Fact]
        public void Should_register_s3_client_factory()
        {
            var services = new ServiceCollection();
            services.ConfigureS3EventStore(new EventStreamS3Settings("s3"));
            Assert.Contains(services, s => s.ServiceType == typeof(IS3ClientFactory));
        }

        [Fact]
        public void Should_register_settings_singleton()
        {
            var services = new ServiceCollection();
            services.ConfigureS3EventStore(new EventStreamS3Settings("s3"));
            Assert.Contains(services, s => s.ServiceType == typeof(EventStreamS3Settings));
        }

        [Fact]
        public void Should_return_service_collection_for_chaining()
        {
            var services = new ServiceCollection();
            var result = services.ConfigureS3EventStore(new EventStreamS3Settings("s3"));
            Assert.Same(services, result);
        }
    }
}
