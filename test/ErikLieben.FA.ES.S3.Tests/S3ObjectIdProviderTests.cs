using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3ObjectIdProviderTests
{
    private static EventStreamS3Settings CreateSettings() =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret");

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3ObjectIdProvider(null!, CreateSettings()));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3ObjectIdProvider(Substitute.For<IS3ClientFactory>(), null!));
        }
    }

    public class GetObjectIdsAsync
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var sut = new S3ObjectIdProvider(Substitute.For<IS3ClientFactory>(), CreateSettings());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetObjectIdsAsync(null!, null, 10));
        }

        [Fact]
        public async Task Should_throw_when_page_size_less_than_1()
        {
            var sut = new S3ObjectIdProvider(Substitute.For<IS3ClientFactory>(), CreateSettings());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                sut.GetObjectIdsAsync("test", null, 0));
        }

        [Fact]
        public async Task Should_return_empty_when_bucket_not_found()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not found") { ErrorCode = "NoSuchBucket" });

            var sut = new S3ObjectIdProvider(clientFactory, CreateSettings());
            var result = await sut.GetObjectIdsAsync("test", null, 10);

            Assert.Empty(result.Items);
            Assert.Null(result.ContinuationToken);
        }

        [Fact]
        public async Task Should_return_object_ids_from_response()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            var response = new ListObjectsV2Response
            {
                S3Objects = new List<S3Object>
                {
                    new() { Key = "test/abc-123.json" },
                    new() { Key = "test/def-456.json" }
                },
                IsTruncated = false
            };
            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(response);

            var sut = new S3ObjectIdProvider(clientFactory, CreateSettings());
            var result = await sut.GetObjectIdsAsync("test", null, 10);

            Assert.Equal(2, result.Items.Count);
            Assert.Contains("abc-123", result.Items);
            Assert.Contains("def-456", result.Items);
        }

        [Fact]
        public async Task Should_return_continuation_token_when_truncated()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            var response = new ListObjectsV2Response
            {
                S3Objects = new List<S3Object>
                {
                    new() { Key = "test/abc.json" },
                },
                IsTruncated = true,
                NextContinuationToken = "next-page-token"
            };
            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(response);

            var sut = new S3ObjectIdProvider(clientFactory, CreateSettings());
            var result = await sut.GetObjectIdsAsync("test", null, 1);

            Assert.Single(result.Items);
            Assert.Equal("next-page-token", result.ContinuationToken);
        }
    }

    public class ExistsAsync
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var sut = new S3ObjectIdProvider(Substitute.For<IS3ClientFactory>(), CreateSettings());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ExistsAsync(null!, "123"));
        }

        [Fact]
        public async Task Should_throw_when_object_id_is_null()
        {
            var sut = new S3ObjectIdProvider(Substitute.For<IS3ClientFactory>(), CreateSettings());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ExistsAsync("test", null!));
        }

        [Fact]
        public async Task Should_return_true_when_object_exists()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse());

            var sut = new S3ObjectIdProvider(clientFactory, CreateSettings());
            var result = await sut.ExistsAsync("test", "123");

            Assert.True(result);
        }

        [Fact]
        public async Task Should_return_false_when_object_not_found()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

            var sut = new S3ObjectIdProvider(clientFactory, CreateSettings());
            var result = await sut.ExistsAsync("test", "123");

            Assert.False(result);
        }
    }

    public class CountAsync
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var sut = new S3ObjectIdProvider(Substitute.For<IS3ClientFactory>(), CreateSettings());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CountAsync(null!));
        }

        [Fact]
        public async Task Should_return_zero_when_bucket_not_found()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not found") { ErrorCode = "NoSuchBucket" });

            var sut = new S3ObjectIdProvider(clientFactory, CreateSettings());
            var result = await sut.CountAsync("test");

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_return_count_of_unique_objects()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            var response = new ListObjectsV2Response
            {
                S3Objects = new List<S3Object>
                {
                    new() { Key = "test/abc.json" },
                    new() { Key = "test/def.json" },
                    new() { Key = "test/ghi.json" },
                },
                IsTruncated = false,
            };
            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(response);

            var sut = new S3ObjectIdProvider(clientFactory, CreateSettings());
            var result = await sut.CountAsync("test");

            Assert.Equal(3, result);
        }
    }
}
