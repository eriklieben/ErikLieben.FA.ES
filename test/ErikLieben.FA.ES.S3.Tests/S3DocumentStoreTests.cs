using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3DocumentStoreTests
{
    private static EventStreamS3Settings CreateSettings(bool autoCreateBucket = true) =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret", autoCreateBucket: autoCreateBucket);

    private static EventStreamDefaultTypeSettings CreateTypeSettings() =>
        new(StreamType: "s3", DocumentType: "s3", DocumentTagType: "s3", EventStreamTagType: "s3", DocumentRefType: "s3");

    private static string CreateDeserializeDocumentJson(string objectId = "123", string objectName = "test")
    {
        var deserializeDoc = new DeserializeS3EventStreamDocument
        {
            ObjectId = objectId,
            ObjectName = objectName,
            Active = new DeserializeS3StreamInformation
            {
                StreamIdentifier = $"{objectId.Replace("-", string.Empty)}0000000000-0000000000",
                StreamType = "s3",
                DocumentType = "s3",
                DocumentTagType = "s3",
                EventStreamTagType = "s3",
                DocumentRefType = "s3",
                CurrentStreamVersion = -1,
                DataStore = "s3",
                DocumentStore = "s3",
                DocumentTagStore = "s3",
                StreamTagStore = "s3",
                SnapShotStore = "s3"
            }
        };
        return JsonSerializer.Serialize(deserializeDoc, DeserializeS3EventStreamDocumentContext.Default.DeserializeS3EventStreamDocument);
    }

    private static IAmazonS3 CreateMockS3Client()
    {
        return Substitute.For<IAmazonS3>();
    }

    private static IS3ClientFactory CreateMockClientFactory(IAmazonS3 s3Client)
    {
        var clientFactory = Substitute.For<IS3ClientFactory>();
        clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);
        return clientFactory;
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3DocumentStore(
                    null!,
                    Substitute.For<IDocumentTagDocumentFactory>(),
                    CreateSettings(),
                    CreateTypeSettings()));
        }

        [Fact]
        public void Should_throw_when_s3_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3DocumentStore(
                    Substitute.For<IS3ClientFactory>(),
                    Substitute.For<IDocumentTagDocumentFactory>(),
                    null!,
                    CreateTypeSettings()));
        }

        [Fact]
        public void Should_throw_when_tag_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3DocumentStore(
                    Substitute.For<IS3ClientFactory>(),
                    null!,
                    CreateSettings(),
                    CreateTypeSettings()));
        }

        [Fact]
        public void Should_throw_when_type_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3DocumentStore(
                    Substitute.For<IS3ClientFactory>(),
                    Substitute.For<IDocumentTagDocumentFactory>(),
                    CreateSettings(),
                    null!));
        }
    }

    public class CreateAsync
    {
        [Fact]
        public async Task Should_create_new_document_when_not_exists()
        {
            // Arrange
            var s3Client = CreateMockS3Client();
            var clientFactory = CreateMockClientFactory(s3Client);
            var tagFactory = Substitute.For<IDocumentTagDocumentFactory>();

            // ObjectExistsAsync calls GetObjectMetadataAsync - return NotFound to indicate not exists
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

            // PutObjectAsEntityAsync calls PutObjectAsync
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse { ETag = "\"new-etag\"" });

            // GetObjectAsEntityAsync calls GetObjectAsync - return the deserialized document
            var json = CreateDeserializeDocumentJson();
            var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectResponse { ResponseStream = responseStream, ETag = "\"etag1\"" });

            // EnsureBucketAsync calls PutBucketAsync
            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutBucketResponse());

            var sut = new S3DocumentStore(clientFactory, tagFactory, CreateSettings(), CreateTypeSettings());

            // Act
            var result = await sut.CreateAsync("test", "123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("123", result.ObjectId);
            Assert.Equal("test", result.ObjectName);
            await s3Client.Received(1).PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_existing_document_when_exists()
        {
            // Arrange
            var s3Client = CreateMockS3Client();
            var clientFactory = CreateMockClientFactory(s3Client);
            var tagFactory = Substitute.For<IDocumentTagDocumentFactory>();

            // ObjectExistsAsync calls GetObjectMetadataAsync - return success to indicate exists
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse());

            // GetObjectAsEntityAsync calls GetObjectAsync - return the deserialized document
            var json = CreateDeserializeDocumentJson();
            var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectResponse { ResponseStream = responseStream, ETag = "\"etag1\"" });

            // EnsureBucketAsync calls PutBucketAsync
            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutBucketResponse());

            var sut = new S3DocumentStore(clientFactory, tagFactory, CreateSettings(), CreateTypeSettings());

            // Act
            var result = await sut.CreateAsync("test", "123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("123", result.ObjectId);
            Assert.Equal("test", result.ObjectName);
            // Should NOT have called PutObjectAsync because document already exists
            await s3Client.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_on_no_such_bucket()
        {
            // Arrange
            var s3Client = CreateMockS3Client();
            var clientFactory = CreateMockClientFactory(s3Client);
            var tagFactory = Substitute.For<IDocumentTagDocumentFactory>();
            var settings = CreateSettings(autoCreateBucket: false);

            // ObjectExistsAsync calls GetObjectMetadataAsync - throw NoSuchBucket
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("No bucket") { ErrorCode = "NoSuchBucket" });

            var sut = new S3DocumentStore(clientFactory, tagFactory, settings, CreateTypeSettings());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.CreateAsync("test", "123")!);
            Assert.Contains("was not found", ex.Message);
        }
    }

    public class GetAsync
    {
        [Fact]
        public async Task Should_return_document_when_found()
        {
            // Arrange
            var s3Client = CreateMockS3Client();
            var clientFactory = CreateMockClientFactory(s3Client);
            var tagFactory = Substitute.For<IDocumentTagDocumentFactory>();

            // GetObjectETagAsync calls GetObjectMetadataAsync
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse { ETag = "\"etag1\"" });

            // GetObjectAsEntityAsync calls GetObjectAsync
            var json = CreateDeserializeDocumentJson();
            var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectResponse { ResponseStream = responseStream, ETag = "\"etag1\"" });

            var sut = new S3DocumentStore(clientFactory, tagFactory, CreateSettings(), CreateTypeSettings());

            // Act
            var result = await sut.GetAsync("test", "123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("123", result.ObjectId);
            Assert.Equal("test", result.ObjectName);
        }

        [Fact]
        public async Task Should_throw_when_document_not_found()
        {
            // Arrange
            var s3Client = CreateMockS3Client();
            var clientFactory = CreateMockClientFactory(s3Client);
            var tagFactory = Substitute.For<IDocumentTagDocumentFactory>();

            // GetObjectETagAsync calls GetObjectMetadataAsync - throw NotFound
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" });

            var sut = new S3DocumentStore(clientFactory, tagFactory, CreateSettings(), CreateTypeSettings());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetAsync("test", "123"));
            Assert.Contains("was not found", ex.Message);
        }

        [Fact]
        public async Task Should_throw_on_no_such_bucket()
        {
            // Arrange
            var s3Client = CreateMockS3Client();
            var clientFactory = CreateMockClientFactory(s3Client);
            var tagFactory = Substitute.For<IDocumentTagDocumentFactory>();

            // GetObjectETagAsync calls GetObjectMetadataAsync - throw NoSuchBucket
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("No bucket") { ErrorCode = "NoSuchBucket" });

            var sut = new S3DocumentStore(clientFactory, tagFactory, CreateSettings(), CreateTypeSettings());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetAsync("test", "123"));
            Assert.Contains("was not found", ex.Message);
        }
    }

    public class SetAsync
    {
        [Fact]
        public async Task Should_throw_when_document_is_null()
        {
            // Arrange
            var sut = new S3DocumentStore(
                Substitute.For<IS3ClientFactory>(),
                Substitute.For<IDocumentTagDocumentFactory>(),
                CreateSettings(),
                CreateTypeSettings());

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetAsync(null!));
        }

        [Fact]
        public async Task Should_save_document_to_s3()
        {
            // Arrange
            var s3Client = CreateMockS3Client();
            var clientFactory = CreateMockClientFactory(s3Client);
            var tagFactory = Substitute.For<IDocumentTagDocumentFactory>();

            var doc = Substitute.For<IObjectDocument>();
            var streamInfo = new StreamInformation
            {
                StreamIdentifier = "abc0000000000-0000000000",
                DocumentStore = "s3"
            };
            doc.Active.Returns(streamInfo);
            doc.ObjectName.Returns("test");
            doc.ObjectId.Returns("123");
            doc.TerminatedStreams.Returns(new List<TerminatedStream>());

            // EnsureBucketAsync calls PutBucketAsync
            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutBucketResponse());

            // GetObjectETagAsync calls GetObjectMetadataAsync
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse { ETag = "\"etag1\"" });

            // PutObjectAsEntityAsync calls PutObjectAsync
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutObjectResponse { ETag = "\"new-etag\"" });

            var sut = new S3DocumentStore(clientFactory, tagFactory, CreateSettings(), CreateTypeSettings());

            // Act
            await sut.SetAsync(doc);

            // Assert
            await s3Client.Received(1).PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
            doc.Received(1).SetHash(Arg.Any<string>(), Arg.Any<string>());
        }
    }
}
