#pragma warning disable CS8625

using Amazon.S3;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3ClientFactoryTests
{
    private static EventStreamS3Settings CreateSettings(
        string serviceUrl = "http://localhost:9000",
        string accessKey = "minioadmin",
        string secretKey = "minioadmin") =>
        new("s3", serviceUrl: serviceUrl, accessKey: accessKey, secretKey: secretKey);

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new S3ClientFactory(null!));
        }
    }

    public class CreateClient
    {
        [Fact]
        public void Should_throw_when_name_is_null()
        {
            var sut = new S3ClientFactory(CreateSettings());
            Assert.Throws<ArgumentNullException>(() => sut.CreateClient(null!));
        }

        [Fact]
        public void Should_throw_when_name_is_empty()
        {
            var sut = new S3ClientFactory(CreateSettings());
            Assert.Throws<ArgumentException>(() => sut.CreateClient(""));
        }

        [Fact]
        public void Should_throw_when_name_is_whitespace()
        {
            var sut = new S3ClientFactory(CreateSettings());
            Assert.Throws<ArgumentException>(() => sut.CreateClient("   "));
        }

        [Fact]
        public void Should_return_s3_client()
        {
            var sut = new S3ClientFactory(CreateSettings());
            var client = sut.CreateClient("test");
            Assert.NotNull(client);
            Assert.IsType<AmazonS3Client>(client);
        }

        [Fact]
        public void Should_cache_clients_by_name()
        {
            var sut = new S3ClientFactory(CreateSettings());
            var client1 = sut.CreateClient("test");
            var client2 = sut.CreateClient("test");
            Assert.Same(client1, client2);
        }

        [Fact]
        public void Should_create_different_clients_for_different_names()
        {
            var sut = new S3ClientFactory(CreateSettings());
            var client1 = sut.CreateClient("client-a");
            var client2 = sut.CreateClient("client-b");
            Assert.NotSame(client1, client2);
        }

        [Fact]
        public void Should_create_client_with_default_credentials_when_no_keys_provided()
        {
            var settings = new EventStreamS3Settings("s3");
            var sut = new S3ClientFactory(settings);
            var client = sut.CreateClient("test");
            Assert.NotNull(client);
        }

        [Fact]
        public void Should_create_client_with_custom_region()
        {
            var settings = new EventStreamS3Settings("s3", region: "eu-west-1");
            var sut = new S3ClientFactory(settings);
            var client = sut.CreateClient("test");
            Assert.NotNull(client);
        }

        [Fact]
        public void Should_create_client_with_max_connections()
        {
            var settings = new EventStreamS3Settings("s3",
                serviceUrl: "http://localhost:9000",
                accessKey: "key",
                secretKey: "secret",
                maxConnectionsPerServer: 50);
            var sut = new S3ClientFactory(settings);
            var client = sut.CreateClient("test");
            Assert.NotNull(client);
        }
    }
}
