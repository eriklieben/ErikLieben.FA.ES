using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.S3.Tests.HealthChecks;

public class S3StorageHealthCheckTests
{
    private static EventStreamS3Settings CreateSettings() =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret");

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3StorageHealthCheck(null!, CreateSettings()));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3StorageHealthCheck(Substitute.For<IS3ClientFactory>(), null!));
        }
    }

    public class CheckHealthAsync
    {
        [Fact]
        public async Task Should_return_healthy_when_s3_accessible()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
                .Returns(new ListBucketsResponse
                {
                    Buckets = new List<S3Bucket> { new() { BucketName = "test" } }
                });

            var sut = new S3StorageHealthCheck(clientFactory, CreateSettings());
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("s3", sut, null, null)
            };

            var result = await sut.CheckHealthAsync(context);

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("BucketCount", result.Data.Keys);
            Assert.Equal(1, result.Data["BucketCount"]);
        }

        [Fact]
        public async Task Should_return_unhealthy_when_s3_not_accessible()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Connection refused"));

            var sut = new S3StorageHealthCheck(clientFactory, CreateSettings());
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("s3", sut, null, null)
            };

            var result = await sut.CheckHealthAsync(context);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("not accessible", result.Description);
        }
    }
}
