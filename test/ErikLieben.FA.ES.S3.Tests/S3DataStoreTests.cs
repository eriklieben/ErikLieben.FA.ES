using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3DataStoreTests
{
    private static EventStreamS3Settings CreateSettings(bool autoCreateBucket = true) =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret", autoCreateBucket: autoCreateBucket);

    private static IObjectDocument CreateDocument(
        string objectName = "test",
        string objectId = "123",
        string streamId = "abc0000000000-0000000000",
        string dataStore = "s3",
        string? hash = null,
        string? prevHash = null)
    {
        var doc = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            DataStore = dataStore,
        };
        doc.Active.Returns(streamInfo);
        doc.ObjectName.Returns(objectName);
        doc.ObjectId.Returns(objectId);
        doc.Hash.Returns(hash);
        doc.PrevHash.Returns(prevHash);
        doc.TerminatedStreams.Returns(new List<TerminatedStream>());
        doc.SchemaVersion.Returns((string?)null);
        return doc;
    }

    private static (IS3ClientFactory Factory, IAmazonS3 Client) CreateMockClient()
    {
        var clientFactory = Substitute.For<IS3ClientFactory>();
        var s3Client = Substitute.For<IAmazonS3>();
        clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);
        return (clientFactory, s3Client);
    }

    private static S3DataStoreDocument CreateDataStoreDocument(
        string objectId = "123",
        string objectName = "test",
        string lastHash = "*",
        params S3JsonEvent[] events)
    {
        var dataDoc = new S3DataStoreDocument
        {
            ObjectId = objectId,
            ObjectName = objectName,
            LastObjectDocumentHash = lastHash
        };
        dataDoc.Events.AddRange(events);
        return dataDoc;
    }

    private static S3JsonEvent CreateS3JsonEvent(int version, string eventType = "Test.Created", string payload = "{}")
    {
        return new S3JsonEvent
        {
            EventVersion = version,
            EventType = eventType,
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static GetObjectResponse CreateGetObjectResponse(S3DataStoreDocument dataDoc, string etag = "\"etag1\"")
    {
        var json = JsonSerializer.Serialize(dataDoc, S3DataStoreDocumentContext.Default.S3DataStoreDocument);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new GetObjectResponse
        {
            ResponseStream = stream,
            ETag = etag
        };
    }

    private static void SetupS3GetObject(IAmazonS3 s3Client, S3DataStoreDocument dataDoc, string etag = "\"etag1\"")
    {
        s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateGetObjectResponse(dataDoc, etag));
    }

    private static void SetupS3GetObjectNotFound(IAmazonS3 s3Client)
    {
        s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });
    }

    private static void SetupS3GetObjectNoSuchBucket(IAmazonS3 s3Client)
    {
        var ex = new AmazonS3Exception("No such bucket") { StatusCode = HttpStatusCode.NotFound };
        // Use reflection or set via the ErrorCode approach
        // AmazonS3Exception.ErrorCode is settable
        ex.ErrorCode = "NoSuchBucket";
        s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Throws(ex);
    }

    private static void SetupObjectExists(IAmazonS3 s3Client, string? etag = "\"etag1\"")
    {
        s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectMetadataResponse { ETag = etag ?? "\"etag1\"" });
    }

    private static void SetupObjectNotExists(IAmazonS3 s3Client)
    {
        s3Client.GetObjectMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });
    }

    private static void SetupPutObject(IAmazonS3 s3Client, string etag = "\"new-etag\"")
    {
        s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = etag });
    }

    private static void SetupEnsureBucket(IAmazonS3 s3Client)
    {
        s3Client.PutBucketAsync(Arg.Any<PutBucketRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutBucketResponse());
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3DataStore(null!, new EventStreamS3Settings("s3")));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3DataStore(Substitute.For<IS3ClientFactory>(), null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = new S3DataStore(
                Substitute.For<IS3ClientFactory>(),
                new EventStreamS3Settings("s3"));

            Assert.NotNull(sut);
        }
    }

    public class ReadAsync
    {
        [Fact]
        public async Task Should_return_null_when_document_not_found()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupS3GetObjectNotFound(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ReadAsync(document);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_all_events_when_no_version_filter()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ReadAsync(document);

            Assert.NotNull(result);
            var events = result!.ToList();
            Assert.Equal(3, events.Count);
        }

        [Fact]
        public async Task Should_return_events_filtered_by_start_version()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ReadAsync(document, startVersion: 1);

            Assert.NotNull(result);
            var events = result!.ToList();
            Assert.Equal(2, events.Count);
            Assert.Equal(1, events[0].EventVersion);
            Assert.Equal(2, events[1].EventVersion);
        }

        [Fact]
        public async Task Should_return_events_filtered_by_until_version()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ReadAsync(document, startVersion: 0, untilVersion: 1);

            Assert.NotNull(result);
            var events = result!.ToList();
            Assert.Equal(2, events.Count);
            Assert.Equal(0, events[0].EventVersion);
            Assert.Equal(1, events[1].EventVersion);
        }

        [Fact]
        public async Task Should_return_events_filtered_by_both_start_and_until_version()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
                CreateS3JsonEvent(3, "Test.Archived"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ReadAsync(document, startVersion: 1, untilVersion: 2);

            Assert.NotNull(result);
            var events = result!.ToList();
            Assert.Equal(2, events.Count);
            Assert.Equal(1, events[0].EventVersion);
            Assert.Equal(2, events[1].EventVersion);
        }

        [Fact]
        public async Task Should_return_null_when_bucket_not_found()
        {
            // The GetObjectAsEntityAsync extension method catches NoSuchBucket
            // and returns (null, null, null), so ReadAsync receives a null document
            // and returns null. The S3DataStore's own NoSuchBucket catch clause is
            // defensive code that would fire if the extension did not handle it.
            var (clientFactory, s3Client) = CreateMockClient();
            SetupS3GetObjectNoSuchBucket(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ReadAsync(document);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_empty_collection_when_document_exists_but_no_events_match()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.ReadAsync(document, startVersion: 5);

            Assert.NotNull(result);
            Assert.Empty(result!);
        }
    }

    public class ReadAsStreamAsync
    {
        [Fact]
        public async Task Should_yield_no_events_when_document_not_found()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupS3GetObjectNotFound(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(document))
            {
                events.Add(evt);
            }

            Assert.Empty(events);
        }

        [Fact]
        public async Task Should_yield_events_from_document()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(document))
            {
                events.Add(evt);
            }

            Assert.Equal(3, events.Count);
            Assert.Equal("Test.Created", events[0].EventType);
            Assert.Equal("Test.Updated", events[1].EventType);
            Assert.Equal("Test.Completed", events[2].EventType);
        }

        [Fact]
        public async Task Should_yield_break_on_bucket_not_found()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupS3GetObjectNoSuchBucket(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(document))
            {
                events.Add(evt);
            }

            Assert.Empty(events);
        }

        [Fact]
        public async Task Should_filter_events_by_start_version()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(document, startVersion: 2))
            {
                events.Add(evt);
            }

            Assert.Single(events);
            Assert.Equal(2, events[0].EventVersion);
        }

        [Fact]
        public async Task Should_filter_events_by_until_version()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var events = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(document, startVersion: 0, untilVersion: 1))
            {
                events.Add(evt);
            }

            Assert.Equal(2, events.Count);
            Assert.Equal(0, events[0].EventVersion);
            Assert.Equal(1, events[1].EventVersion);
        }

        [Fact]
        public async Task Should_respect_cancellation_token()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var cts = new CancellationTokenSource();
            var events = new List<IEvent>();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var evt in sut.ReadAsStreamAsync(document, cancellationToken: cts.Token))
                {
                    events.Add(evt);
                    cts.Cancel(); // Cancel after first event
                }
            });

            Assert.Single(events);
        }
    }

    public class AppendAsync
    {
        [Fact]
        public async Task Should_throw_when_document_is_null()
        {
            var (clientFactory, _) = CreateMockClient();
            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);

            var evt = CreateS3JsonEvent(0);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.AppendAsync(null!, CancellationToken.None, evt));
        }

        [Fact]
        public async Task Should_throw_when_events_array_is_null()
        {
            var (clientFactory, _) = CreateMockClient();
            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.AppendAsync(document, CancellationToken.None, null!));
        }

        [Fact]
        public async Task Should_throw_when_no_events_provided()
        {
            var (clientFactory, _) = CreateMockClient();
            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.AppendAsync(document, CancellationToken.None, Array.Empty<IEvent>()));

            Assert.Contains("No events provided", ex.Message);
        }

        [Fact]
        public async Task Should_throw_when_stream_identifier_is_null()
        {
            var (clientFactory, _) = CreateMockClient();
            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);

            var doc = Substitute.For<IObjectDocument>();
            var streamInfo = new StreamInformation
            {
                StreamIdentifier = null!,
                DataStore = "s3",
            };
            doc.Active.Returns(streamInfo);
            doc.ObjectName.Returns("test");
            doc.ObjectId.Returns("123");
            doc.TerminatedStreams.Returns(new List<TerminatedStream>());

            var evt = CreateS3JsonEvent(0);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.AppendAsync(doc, CancellationToken.None, evt));
        }

        [Fact]
        public async Task Should_create_new_document_when_not_exists()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);
            SetupS3GetObjectNotFound(s3Client);
            SetupPutObject(s3Client);

            var settings = CreateSettings(autoCreateBucket: true);
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var evt = CreateS3JsonEvent(0, "Test.Created", """{"name":"test"}""");

            await sut.AppendAsync(document, CancellationToken.None, evt);

            await s3Client.Received(1).PutObjectAsync(
                Arg.Is<PutObjectRequest>(r => r.BucketName == "test"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_create_bucket_when_auto_create_enabled()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);
            SetupS3GetObjectNotFound(s3Client);
            SetupPutObject(s3Client);

            var settings = CreateSettings(autoCreateBucket: true);
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var evt = CreateS3JsonEvent(0);

            await sut.AppendAsync(document, CancellationToken.None, evt);

            await s3Client.Received().PutBucketAsync(
                Arg.Is<PutBucketRequest>(r => r.BucketName == "test"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_create_bucket_when_auto_create_disabled()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupS3GetObjectNotFound(s3Client);
            SetupPutObject(s3Client);

            var settings = CreateSettings(autoCreateBucket: false);
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var evt = CreateS3JsonEvent(0);

            await sut.AppendAsync(document, CancellationToken.None, evt);

            await s3Client.DidNotReceive().PutBucketAsync(
                Arg.Any<PutBucketRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_append_to_existing_document()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);

            // Object exists
            SetupObjectExists(s3Client, "\"etag1\"");

            // Return existing document when downloading
            var existingDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
            ]);
            SetupS3GetObject(s3Client, existingDoc, "\"etag1\"");
            SetupPutObject(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument(hash: "somehash");

            var newEvent = CreateS3JsonEvent(1, "Test.Updated");

            await sut.AppendAsync(document, CancellationToken.None, newEvent);

            // Verify PutObjectAsync was called (update to existing doc)
            await s3Client.Received().PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_when_stream_is_closed()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);

            // Object exists
            SetupObjectExists(s3Client, "\"etag1\"");

            // Return existing document with EventStream.Closed as last event
            var existingDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "EventStream.Closed"),
            ]);
            existingDoc.Events[1] = new S3JsonEvent
            {
                EventVersion = 1,
                EventType = "EventStream.Closed",
                Payload = "{}",
                Timestamp = DateTimeOffset.UtcNow
            };
            SetupS3GetObject(s3Client, existingDoc, "\"etag1\"");

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var newEvent = CreateS3JsonEvent(2, "Test.Updated");

            var ex = await Assert.ThrowsAsync<EventStreamClosedException>(() =>
                sut.AppendAsync(document, CancellationToken.None, newEvent));

            Assert.Contains("closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Should_throw_when_hash_mismatch_on_existing_document()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);

            // Object exists
            SetupObjectExists(s3Client, "\"etag1\"");

            // Return existing document with a specific hash
            var existingDoc = CreateDataStoreDocument(lastHash: "different-hash", events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
            ]);
            SetupS3GetObject(s3Client, existingDoc, "\"etag1\"");

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            // Document has a prevHash that does NOT match the document's LastObjectDocumentHash
            var document = CreateDocument(hash: "newhash", prevHash: "wrong-hash");

            var newEvent = CreateS3JsonEvent(1, "Test.Updated");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.AppendAsync(document, CancellationToken.None, newEvent));

            Assert.Contains("concurrency", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Should_skip_hash_check_when_existing_hash_is_wildcard()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);

            // Object exists
            SetupObjectExists(s3Client, "\"etag1\"");

            // Return existing document with wildcard hash
            var existingDoc = CreateDataStoreDocument(lastHash: "*", events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
            ]);
            SetupS3GetObject(s3Client, existingDoc, "\"etag1\"");
            SetupPutObject(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument(hash: "somehash", prevHash: "anyhash");

            var newEvent = CreateS3JsonEvent(1, "Test.Updated");

            // Should not throw - wildcard hash bypasses concurrency check
            await sut.AppendAsync(document, CancellationToken.None, newEvent);

            await s3Client.Received().PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_InvalidOperationException_when_bucket_not_found_on_new_document()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);
            SetupS3GetObjectNotFound(s3Client);

            // PutObjectAsync throws NoSuchBucket
            var bucketEx = new AmazonS3Exception("No such bucket") { ErrorCode = "NoSuchBucket" };
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Throws(bucketEx);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var evt = CreateS3JsonEvent(0);

            // The extension method PutObjectAsEntityAsync catches NoSuchBucket and throws InvalidOperationException
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.AppendAsync(document, CancellationToken.None, evt));

            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Should_use_correct_bucket_name_from_object_name()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);
            SetupS3GetObjectNotFound(s3Client);
            SetupPutObject(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument(objectName: "MyAggregate");

            var evt = CreateS3JsonEvent(0);

            await sut.AppendAsync(document, CancellationToken.None, evt);

            // bucket name should be lowercased object name
            await s3Client.Received().PutObjectAsync(
                Arg.Is<PutObjectRequest>(r => r.BucketName == "myaggregate"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_append_multiple_events_at_once()
        {
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);
            SetupS3GetObjectNotFound(s3Client);
            SetupPutObject(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var events = new IEvent[]
            {
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            };

            await sut.AppendAsync(document, CancellationToken.None, events);

            // Verify the PutObjectAsync was called once with all events
            await s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class AppendAsyncRoundTripReduction
    {
        [Fact]
        public async Task Should_download_object_directly_without_separate_exists_check()
        {
            // Arrange - The new flow calls GetObjectAsEntityAsync directly, which
            // returns (null, null, null) on 404, instead of calling ObjectExistsAsync first.
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);

            // GetObjectAsync returns 404 (object doesn't exist)
            SetupS3GetObjectNotFound(s3Client);
            SetupPutObject(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var evt = CreateS3JsonEvent(0, "Test.Created");

            // Act
            await sut.AppendAsync(document, CancellationToken.None, evt);

            // Assert - GetObjectAsync was called (for download), not GetObjectMetadataAsync (no separate Exists check)
            await s3Client.Received().GetObjectAsync(
                Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>());
            await s3Client.DidNotReceive().GetObjectMetadataAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_use_etag_from_download_for_optimistic_concurrency()
        {
            // Arrange - When the object exists, GetObjectAsEntityAsync returns
            // both the document and the ETag from the same request.
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);

            var existingDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
            ]);
            SetupS3GetObject(s3Client, existingDoc, "\"download-etag\"");

            PutObjectRequest? capturedRequest = null;
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    capturedRequest = callInfo.Arg<PutObjectRequest>();
                    return new PutObjectResponse { ETag = "\"new-etag\"" };
                });

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument(hash: "somehash");

            var newEvent = CreateS3JsonEvent(1, "Test.Updated");

            // Act
            await sut.AppendAsync(document, CancellationToken.None, newEvent);

            // Assert - PutObjectAsync should be called
            Assert.NotNull(capturedRequest);
            await s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_cache_bucket_verification_across_calls()
        {
            // Arrange - VerifiedBuckets is a static ConcurrentDictionary that
            // caches bucket names once verified. The second append to the same
            // bucket should not call EnsureBucketAsync again.
            var (clientFactory, s3Client) = CreateMockClient();
            SetupEnsureBucket(s3Client);
            SetupS3GetObjectNotFound(s3Client);
            SetupPutObject(s3Client);

            // Use unique bucket name to avoid interference from other tests
            var uniqueName = $"cachetest{Guid.NewGuid():N}".Substring(0, 20);
            var settings = CreateSettings(autoCreateBucket: true);
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument(objectName: uniqueName);

            var evt1 = CreateS3JsonEvent(0, "Test.Created");
            var evt2 = CreateS3JsonEvent(1, "Test.Updated");

            // Act - Two appends to same bucket
            await sut.AppendAsync(document, CancellationToken.None, evt1);
            await sut.AppendAsync(document, CancellationToken.None, evt2);

            // Assert - PutBucketAsync should only be called once (cached after first call)
            await s3Client.Received(1).PutBucketAsync(
                Arg.Is<PutBucketRequest>(r => r.BucketName == uniqueName),
                Arg.Any<CancellationToken>());
        }
    }

    public class RemoveEventsForFailedCommitAsync
    {
        [Fact]
        public async Task Should_return_zero_when_object_not_found()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            // GetObjectMetadataAsync throws NotFound (GetObjectETagAsync returns null)
            SetupObjectNotExists(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.RemoveEventsForFailedCommitAsync(document, fromVersion: 1, toVersion: 3);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_return_zero_when_no_events_in_document()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            // ETag exists
            SetupObjectExists(s3Client, "\"etag1\"");

            // Return document with no events
            var emptyDoc = CreateDataStoreDocument();
            SetupS3GetObject(s3Client, emptyDoc, "\"etag1\"");

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.RemoveEventsForFailedCommitAsync(document, fromVersion: 0, toVersion: 5);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_remove_events_in_version_range()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            // ETag exists
            SetupObjectExists(s3Client, "\"etag1\"");

            // Return document with events spanning versions 0-4
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Modified"),
                CreateS3JsonEvent(3, "Test.Changed"),
                CreateS3JsonEvent(4, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc, "\"etag1\"");
            SetupPutObject(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.RemoveEventsForFailedCommitAsync(document, fromVersion: 2, toVersion: 3);

            Assert.Equal(2, result);

            // Verify the document was rewritten
            await s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_rewrite_when_no_events_removed()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            // ETag exists
            SetupObjectExists(s3Client, "\"etag1\"");

            // Return document with events at versions 0-2
            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc, "\"etag1\"");

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            // Remove versions 10-20 which don't exist
            var result = await sut.RemoveEventsForFailedCommitAsync(document, fromVersion: 10, toVersion: 20);

            Assert.Equal(0, result);

            // PutObjectAsync should NOT have been called since nothing was removed
            await s3Client.DidNotReceive().PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_zero_when_s3_exception_with_not_found()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            // ETag exists (GetObjectMetadataAsync succeeds)
            SetupObjectExists(s3Client, "\"etag1\"");

            // But GetObjectAsync throws NotFound
            s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Throws(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.RemoveEventsForFailedCommitAsync(document, fromVersion: 0, toVersion: 5);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_return_zero_when_s3_exception_with_no_such_key()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            // ETag exists
            SetupObjectExists(s3Client, "\"etag1\"");

            // But GetObjectAsync throws NoSuchKey
            var ex = new AmazonS3Exception("No such key") { ErrorCode = "NoSuchKey", StatusCode = HttpStatusCode.NotFound };
            s3Client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Throws(ex);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.RemoveEventsForFailedCommitAsync(document, fromVersion: 0, toVersion: 5);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_throw_when_document_is_null()
        {
            var (clientFactory, _) = CreateMockClient();
            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RemoveEventsForFailedCommitAsync(null!, fromVersion: 0, toVersion: 5));
        }

        [Fact]
        public async Task Should_remove_all_events_when_range_covers_all()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            // ETag exists
            SetupObjectExists(s3Client, "\"etag1\"");

            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc, "\"etag1\"");
            SetupPutObject(s3Client);

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.RemoveEventsForFailedCommitAsync(document, fromVersion: 0, toVersion: 2);

            Assert.Equal(3, result);

            // Document should be rewritten with empty events list
            await s3Client.Received(1).PutObjectAsync(
                Arg.Any<PutObjectRequest>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_keep_events_outside_the_version_range()
        {
            var (clientFactory, s3Client) = CreateMockClient();

            // ETag exists
            SetupObjectExists(s3Client, "\"etag1\"");

            var dataDoc = CreateDataStoreDocument(events:
            [
                CreateS3JsonEvent(0, "Test.Created"),
                CreateS3JsonEvent(1, "Test.Updated"),
                CreateS3JsonEvent(2, "Test.Modified"),
                CreateS3JsonEvent(3, "Test.Changed"),
                CreateS3JsonEvent(4, "Test.Completed"),
            ]);
            SetupS3GetObject(s3Client, dataDoc, "\"etag1\"");

            PutObjectRequest? capturedRequest = null;
            s3Client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    capturedRequest = callInfo.Arg<PutObjectRequest>();
                    return new PutObjectResponse { ETag = "\"new-etag\"" };
                });

            var settings = CreateSettings();
            var sut = new S3DataStore(clientFactory, settings);
            var document = CreateDocument();

            var result = await sut.RemoveEventsForFailedCommitAsync(document, fromVersion: 1, toVersion: 3);

            Assert.Equal(3, result);
            Assert.NotNull(capturedRequest);

            // Read back the document that was written to verify remaining events
            using var reader = new StreamReader(capturedRequest!.InputStream);
            capturedRequest.InputStream.Position = 0;
            var writtenJson = await reader.ReadToEndAsync();
            var writtenDoc = JsonSerializer.Deserialize(writtenJson, S3DataStoreDocumentContext.Default.S3DataStoreDocument);

            Assert.NotNull(writtenDoc);
            Assert.Equal(2, writtenDoc!.Events.Count);
            Assert.Equal(0, writtenDoc.Events[0].EventVersion);
            Assert.Equal(4, writtenDoc.Events[1].EventVersion);
        }
    }
}
