using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3SnapShotStoreTests
{
    private static EventStreamS3Settings CreateSettings() =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret");

    private static IObjectDocument CreateDocument(string objectName = "test", string objectId = "123", string streamId = "abc0000000000")
    {
        var doc = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            SnapShotStore = "s3"
        };
        doc.Active.Returns(streamInfo);
        doc.ObjectName.Returns(objectName);
        doc.ObjectId.Returns(objectId);
        return doc;
    }

    public class DeleteAsync
    {
        [Fact]
        public async Task Should_return_false_when_snapshot_does_not_exist()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

            var settings = CreateSettings();
            var sut = new S3SnapShotStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.DeleteAsync(document, 5);

            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_true_when_snapshot_deleted()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            // Object exists
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse());

            // EnsureBucket
            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutBucketResponse());

            var settings = CreateSettings();
            var sut = new S3SnapShotStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.DeleteAsync(document, 5);

            Assert.True(result);
            await s3Client.Received(1).DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    public class DeleteManyAsync
    {
        [Fact]
        public async Task Should_return_count_of_deleted_snapshots()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            // All exist
            s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse());

            // EnsureBucket
            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutBucketResponse());

            var settings = CreateSettings();
            var sut = new S3SnapShotStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.DeleteManyAsync(document, [1, 2, 3]);

            Assert.Equal(3, result);
        }
    }

    public class ListSnapshotsAsync
    {
        [Fact]
        public async Task Should_return_empty_list_when_no_snapshots()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutBucketResponse());

            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>(),
                    IsTruncated = false
                });

            var settings = CreateSettings();
            var sut = new S3SnapShotStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ListSnapshotsAsync(document);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_snapshots_ordered_by_version_descending()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
                .Returns(new PutBucketResponse());

            s3Client.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
                .Returns(new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>
                    {
                        new() { Key = "snapshot/abc0000000000-00000000000000000005.json", Size = 100, LastModified = DateTime.UtcNow },
                        new() { Key = "snapshot/abc0000000000-00000000000000000010.json", Size = 200, LastModified = DateTime.UtcNow },
                        new() { Key = "snapshot/abc0000000000-00000000000000000001.json", Size = 50, LastModified = DateTime.UtcNow },
                    },
                    IsTruncated = false
                });

            var settings = CreateSettings();
            var sut = new S3SnapShotStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ListSnapshotsAsync(document);

            Assert.Equal(3, result.Count);
            Assert.Equal(10, result[0].Version);
            Assert.Equal(5, result[1].Version);
            Assert.Equal(1, result[2].Version);
        }
    }
}
