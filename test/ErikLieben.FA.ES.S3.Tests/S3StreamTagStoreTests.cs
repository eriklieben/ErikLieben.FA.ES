using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3StreamTagStoreTests
{
    private static EventStreamS3Settings CreateSettings(bool autoCreateBucket = true) =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret", autoCreateBucket: autoCreateBucket);

    private static IObjectDocument CreateDocument(
        string objectName = "test",
        string objectId = "obj-123",
        string streamId = "stream-001",
        string dataStore = "s3")
    {
        var doc = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            DataStore = dataStore
        };
        doc.Active.Returns(streamInfo);
        doc.ObjectName.Returns(objectName);
        doc.ObjectId.Returns(objectId);
        return doc;
    }

    private static S3StreamTagStore CreateSut(
        IS3ClientFactory clientFactory,
        EventStreamS3Settings? settings = null)
    {
        return new S3StreamTagStore(
            clientFactory,
            "s3",
            settings ?? CreateSettings());
    }

    private static (IS3ClientFactory Factory, IAmazonS3 Client) CreateMockClient()
    {
        var clientFactory = Substitute.For<IS3ClientFactory>();
        var s3Client = Substitute.For<IAmazonS3>();
        clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);
        return (clientFactory, s3Client);
    }

    private static void MockEnsureBucket(IAmazonS3 s3Client)
    {
        s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutBucketResponse());
    }

    private static void MockObjectExists(IAmazonS3 s3Client)
    {
        s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectMetadataResponse());
    }

    private static void MockObjectNotExists(IAmazonS3 s3Client)
    {
        s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });
    }

    private static void MockPutObject(IAmazonS3 s3Client, string etag = "\"etag\"")
    {
        s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = etag });
    }

    private static void MockGetObjectWithDocument(IAmazonS3 s3Client, S3DocumentTagStoreDocument tagDoc, string etag = "\"etag1\"")
    {
        var json = JsonSerializer.Serialize(tagDoc, S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = stream, ETag = etag });
    }

    private static void MockGetObjectNotFound(IAmazonS3 s3Client)
    {
        s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });
    }

    public class SetAsync
    {
        [Fact]
        public async Task Should_throw_when_document_is_null()
        {
            var (clientFactory, _) = CreateMockClient();
            var sut = CreateSut(clientFactory);

            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!, "my-tag"));
        }

        [Fact]
        public async Task Should_throw_when_tag_is_empty()
        {
            var (clientFactory, _) = CreateMockClient();
            var sut = CreateSut(clientFactory);
            var document = CreateDocument();

            await Assert.ThrowsAsync<ArgumentException>(() => sut.SetAsync(document, ""));
        }

        [Fact]
        public async Task Should_create_new_tag_when_not_exists()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            MockEnsureBucket(s3Client);
            MockObjectNotExists(s3Client);
            MockPutObject(s3Client);

            var sut = CreateSut(clientFactory);
            var document = CreateDocument();

            await sut.SetAsync(document, "my-tag");

            await s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r =>
                    r.BucketName == "test" &&
                    r.Key == "tags/stream-by-tag/my-tag.json"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_add_to_existing_tag()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            MockEnsureBucket(s3Client);
            MockObjectExists(s3Client);

            var existingDoc = new S3DocumentTagStoreDocument
            {
                Tag = "my-tag",
                ObjectIds = ["existing-stream"]
            };
            MockGetObjectWithDocument(s3Client, existingDoc);
            MockPutObject(s3Client);

            var sut = CreateSut(clientFactory);
            var document = CreateDocument(streamId: "new-stream");

            await sut.SetAsync(document, "my-tag");

            await s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r =>
                    r.BucketName == "test" &&
                    r.Key == "tags/stream-by-tag/my-tag.json"),
                Arg.Any<CancellationToken>());
        }
    }

    public class GetAsync
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_empty()
        {
            var (clientFactory, _) = CreateMockClient();
            var sut = CreateSut(clientFactory);

            await Assert.ThrowsAsync<ArgumentException>(() => sut.GetAsync("", "my-tag"));
        }

        [Fact]
        public async Task Should_throw_when_tag_is_empty()
        {
            var (clientFactory, _) = CreateMockClient();
            var sut = CreateSut(clientFactory);

            await Assert.ThrowsAsync<ArgumentException>(() => sut.GetAsync("test", ""));
        }

        [Fact]
        public async Task Should_return_empty_when_not_found()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            MockGetObjectNotFound(s3Client);

            var sut = CreateSut(clientFactory);

            var result = await sut.GetAsync("test", "nonexistent-tag");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_stream_ids_when_found()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            var tagDoc = new S3DocumentTagStoreDocument
            {
                Tag = "my-tag",
                ObjectIds = ["stream-1", "stream-2", "stream-3"]
            };
            MockGetObjectWithDocument(s3Client, tagDoc);

            var sut = CreateSut(clientFactory);

            var result = await sut.GetAsync("test", "my-tag");

            Assert.Equal(["stream-1", "stream-2", "stream-3"], result);
        }
    }

    public class RemoveAsync
    {
        [Fact]
        public async Task Should_throw_when_document_is_null()
        {
            var (clientFactory, _) = CreateMockClient();
            var sut = CreateSut(clientFactory);

            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RemoveAsync(null!, "my-tag"));
        }

        [Fact]
        public async Task Should_do_nothing_when_not_found()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            MockGetObjectNotFound(s3Client);

            var sut = CreateSut(clientFactory);
            var document = CreateDocument();

            await sut.RemoveAsync(document, "nonexistent-tag");

            await s3Client.DidNotReceive().DeleteObjectAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await s3Client.DidNotReceive().PutObjectAsync(
                Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_delete_when_last_stream_removed()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            var tagDoc = new S3DocumentTagStoreDocument
            {
                Tag = "my-tag",
                ObjectIds = ["stream-001"]
            };
            MockGetObjectWithDocument(s3Client, tagDoc);

            var sut = CreateSut(clientFactory);
            var document = CreateDocument(streamId: "stream-001");

            await sut.RemoveAsync(document, "my-tag");

            await s3Client.Received(1).DeleteObjectAsync(
                "test", "tags/stream-by-tag/my-tag.json", Arg.Any<CancellationToken>());
        }
    }
}
