using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.S3.Extensions;
using ErikLieben.FA.ES.S3.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ErikLieben.FA.ES.S3.Tests.Extensions;

public class S3ExtensionsTests
{
    public class ComputeSha256Hash
    {
        [Fact]
        public void Should_compute_hash_for_string()
        {
            var hash = S3Extensions.ComputeSha256Hash("hello");
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            Assert.Equal(64, hash.Length); // SHA256 = 32 bytes = 64 hex chars
        }

        [Fact]
        public void Should_return_same_hash_for_same_input()
        {
            var hash1 = S3Extensions.ComputeSha256Hash("test data");
            var hash2 = S3Extensions.ComputeSha256Hash("test data");
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void Should_return_different_hash_for_different_input()
        {
            var hash1 = S3Extensions.ComputeSha256Hash("data1");
            var hash2 = S3Extensions.ComputeSha256Hash("data2");
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Should_compute_hash_for_byte_array()
        {
            var data = Encoding.UTF8.GetBytes("hello");
            var hash = S3Extensions.ComputeSha256Hash(data, 0, data.Length);
            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length);
        }

        [Fact]
        public void Should_compute_consistent_hash_between_string_and_bytes()
        {
            var text = "consistent hash test";
            var hashFromString = S3Extensions.ComputeSha256Hash(text);
            var bytes = Encoding.UTF8.GetBytes(text);
            var hashFromBytes = S3Extensions.ComputeSha256Hash(bytes, 0, bytes.Length);
            Assert.Equal(hashFromString, hashFromBytes);
        }
    }

    public class ObjectExistsAsync
    {
        [Fact]
        public async Task Should_return_true_when_object_exists()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse());

            var result = await s3Client.ObjectExistsAsync("bucket", "key");

            Assert.True(result);
        }

        [Fact]
        public async Task Should_return_false_when_not_found()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            var result = await s3Client.ObjectExistsAsync("bucket", "key");

            Assert.False(result);
        }
    }

    public class GetObjectETagAsync
    {
        [Fact]
        public async Task Should_return_etag_when_exists()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse { ETag = "\"abc123\"" });

            var result = await s3Client.GetObjectETagAsync("bucket", "key");

            Assert.Equal("\"abc123\"", result);
        }

        [Fact]
        public async Task Should_return_null_when_not_found()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            var result = await s3Client.GetObjectETagAsync("bucket", "key");

            Assert.Null(result);
        }
    }

    public class EnsureBucketAsync
    {
        [Fact]
        public async Task Should_create_bucket()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutBucketResponse());

            await s3Client.EnsureBucketAsync("my-bucket");

            await s3Client.Received(1).PutBucketAsync(
                Arg.Is<PutBucketRequest>(r => r.BucketName == "my-bucket"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_ignore_bucket_already_exists()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Bucket already exists") { ErrorCode = "BucketAlreadyOwnedByYou" });

            // Should not throw
            await s3Client.EnsureBucketAsync("my-bucket");
        }
    }

    public class GetObjectAsStringAsync
    {
        [Fact]
        public async Task Should_return_content_as_string()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            var content = "hello world";
            s3Client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectResponse { ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(content)) });

            var result = await s3Client.GetObjectAsStringAsync("bucket", "key");

            Assert.Equal(content, result);
        }

        [Fact]
        public async Task Should_return_null_when_not_found()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            var result = await s3Client.GetObjectAsStringAsync("bucket", "key");

            Assert.Null(result);
        }
    }

    public class GetObjectAsEntityAsyncTyped
    {
        [Fact]
        public async Task Should_return_entity_when_found()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            var entity = new S3DataStoreDocument { ObjectId = "id", ObjectName = "test", LastObjectDocumentHash = "*" };
            var json = JsonSerializer.Serialize(entity, S3DataStoreDocumentContext.Default.S3DataStoreDocument);

            s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectResponse
                {
                    ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(json)),
                    ETag = "\"etag123\""
                });

            var (document, hash, etag) = await s3Client.GetObjectAsEntityAsync(
                "bucket", "key", S3DataStoreDocumentContext.Default.S3DataStoreDocument);

            Assert.NotNull(document);
            Assert.Equal("id", document.ObjectId);
            Assert.Equal("test", document.ObjectName);
            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length);
            Assert.Equal("\"etag123\"", etag);
        }

        [Fact]
        public async Task Should_return_null_tuple_when_not_found()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

            var (document, hash, etag) = await s3Client.GetObjectAsEntityAsync(
                "bucket", "key", S3DataStoreDocumentContext.Default.S3DataStoreDocument);

            Assert.Null(document);
            Assert.Null(hash);
            Assert.Null(etag);
        }

        [Fact]
        public async Task Should_throw_on_precondition_failed()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Precondition Failed") { StatusCode = HttpStatusCode.PreconditionFailed });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                s3Client.GetObjectAsEntityAsync(
                    "bucket", "key", S3DataStoreDocumentContext.Default.S3DataStoreDocument, ifMatchETag: "\"old-etag\""));
        }
    }

    public class PutObjectAsEntityAsync
    {
        [Fact]
        public async Task Should_return_etag_and_hash()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse { ETag = "\"new-etag\"" });

            var entity = new S3DataStoreDocument { ObjectId = "id", ObjectName = "test", LastObjectDocumentHash = "*" };

            var (etag, hash) = await s3Client.PutObjectAsEntityAsync(
                "bucket", "key", entity, S3DataStoreDocumentContext.Default.S3DataStoreDocument);

            Assert.Equal("\"new-etag\"", etag);
            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length);
        }

        [Fact]
        public async Task Should_throw_on_no_such_bucket()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("No such bucket") { ErrorCode = "NoSuchBucket" });

            var entity = new S3DataStoreDocument { ObjectId = "id", ObjectName = "test", LastObjectDocumentHash = "*" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                s3Client.PutObjectAsEntityAsync(
                    "bucket", "key", entity, S3DataStoreDocumentContext.Default.S3DataStoreDocument));
        }
    }

    public class PutObjectAsync
    {
        [Fact]
        public async Task Should_return_etag()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse { ETag = "\"put-etag\"" });

            var entity = new S3DataStoreDocument { ObjectId = "id", ObjectName = "test", LastObjectDocumentHash = "*" };

            var etag = await s3Client.PutObjectAsync(
                "bucket", "key", entity,
                (System.Text.Json.Serialization.Metadata.JsonTypeInfo)S3DataStoreDocumentContext.Default.S3DataStoreDocument);

            Assert.Equal("\"put-etag\"", etag);
        }

        [Fact]
        public async Task Should_throw_on_no_such_bucket()
        {
            var s3Client = Substitute.For<IAmazonS3>();
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("No such bucket") { ErrorCode = "NoSuchBucket" });

            var entity = new S3DataStoreDocument { ObjectId = "id", ObjectName = "test", LastObjectDocumentHash = "*" };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                s3Client.PutObjectAsync(
                    "bucket", "key", entity,
                    (System.Text.Json.Serialization.Metadata.JsonTypeInfo)S3DataStoreDocumentContext.Default.S3DataStoreDocument));
        }
    }
}
