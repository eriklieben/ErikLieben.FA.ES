using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3StreamMetadataProviderTests
{
    private static EventStreamS3Settings CreateSettings() =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret");

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3StreamMetadataProvider(null!, CreateSettings()));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3StreamMetadataProvider(Substitute.For<IS3ClientFactory>(), null!));
        }
    }

    public class GetStreamMetadataAsync
    {
        [Fact]
        public async Task Should_return_null_when_no_objects()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>(),
                    IsTruncated = false
                });

            var sut = new S3StreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("test", "123");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_null_when_bucket_not_found()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

            var sut = new S3StreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("test", "123");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_metadata_with_event_count()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            var now = DateTime.UtcNow;
            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>
                    {
                        new() { Key = "test/123-stream.json", LastModified = now.AddHours(-1) },
                        new() { Key = "test/123-events.json", LastModified = now },
                    },
                    IsTruncated = false
                });

            var sut = new S3StreamMetadataProvider(clientFactory, CreateSettings());
            var result = await sut.GetStreamMetadataAsync("test", "123");

            Assert.NotNull(result);
            Assert.Equal(2, result.EventCount);
            Assert.Equal("test", result.ObjectName);
            Assert.Equal("123", result.ObjectId);
        }

        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var sut = new S3StreamMetadataProvider(Substitute.For<IS3ClientFactory>(), CreateSettings());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetStreamMetadataAsync(null!, "123"));
        }

        [Fact]
        public async Task Should_throw_when_object_id_is_null()
        {
            var sut = new S3StreamMetadataProvider(Substitute.For<IS3ClientFactory>(), CreateSettings());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetStreamMetadataAsync("test", null!));
        }
    }
}
