#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS0618 // Type or member is obsolete - testing deprecated API intentionally

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.AzureStorage.AppendBlob;
using ErikLieben.FA.ES.AzureStorage.AppendBlob.Model;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Extensions.Azure;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.AppendBlob;

/// <summary>
/// JSON context for test serialization of BlobJsonEvent (the internal contexts are not accessible from tests).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BlobJsonEvent))]
[JsonSerializable(typeof(AppendBlobCommitMarker))]
internal partial class AppendBlobTestJsonContext : JsonSerializerContext
{
}

public class AppendBlobDataStoreTests
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly BlobServiceClient blobServiceClient;
    private readonly BlobContainerClient containerClient;
    private readonly AppendBlobClient appendBlobClient;
    private readonly IObjectDocument objectDocument;
    private readonly IEvent[] events;

    public AppendBlobDataStoreTests()
    {
        AppendBlobDataStore.ClearVerifiedContainersCache();

        clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
        blobServiceClient = Substitute.For<BlobServiceClient>();
        containerClient = Substitute.For<BlobContainerClient>();
        appendBlobClient = Substitute.For<AppendBlobClient>();
        objectDocument = Substitute.For<IObjectDocument>();
        var streamInformation = Substitute.For<StreamInformation>();
        events = [CreateTestEvent(1)];

        // Setup default stream information
        streamInformation.StreamIdentifier = "test-stream";
        streamInformation.StreamConnectionName = "test-connection";
        streamInformation.DataStore = "test-connection";
        streamInformation.CurrentStreamVersion = 10;
        streamInformation.ChunkSettings = new StreamChunkSettings
        {
            EnableChunks = false
        };

        // Setup default object document
        objectDocument.Active.Returns(streamInformation);
        objectDocument.ObjectName.Returns("TestObject");
        objectDocument.ObjectId.Returns("test-id");
        objectDocument.Hash.Returns("hash-abc");
        objectDocument.PrevHash.Returns("hash-prev");
        objectDocument.TerminatedStreams.Returns([]);

        // Setup blob client chain
        clientFactory.CreateClient("test-connection").Returns(blobServiceClient);
        blobServiceClient.GetBlobContainerClient("testobject").Returns(containerClient);
        containerClient.GetAppendBlobClient(Arg.Any<string>()).Returns(appendBlobClient);
    }

    private static JsonEvent CreateTestEvent(int version, string eventType = "Test.Event")
    {
        return new BlobJsonEvent
        {
            EventVersion = version,
            EventType = eventType,
            SchemaVersion = 1,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = "{}"
        };
    }

    private static string CreateNdjsonContent(params (int version, string eventType)[] eventDefs)
    {
        var sb = new StringBuilder();
        foreach (var (version, eventType) in eventDefs)
        {
            var evt = new BlobJsonEvent
            {
                EventVersion = version,
                EventType = eventType,
                SchemaVersion = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Payload = "{}"
            };
            sb.AppendLine(JsonSerializer.Serialize(evt, AppendBlobTestJsonContext.Default.BlobJsonEvent));
        }
        return sb.ToString();
    }

    private static string CreateCommitMarkerLine(string hash, string prevHash, int version)
    {
        var marker = new AppendBlobCommitMarker
        {
            Hash = hash,
            PrevHash = prevHash,
            Version = version
        };
        return JsonSerializer.Serialize(marker, AppendBlobTestJsonContext.Default.AppendBlobCommitMarker);
    }

    private void SetupDownloadStreamingToReturn(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var memStream = new MemoryStream(bytes);
                var downloadResult = BlobsModelFactory.BlobDownloadStreamingResult(content: memStream);
                return Response.FromValue(downloadResult, Substitute.For<Response>());
            });
    }

    public class Constructor : AppendBlobDataStoreTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new AppendBlobDataStore(null!, false));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = new AppendBlobDataStore(clientFactory, true);
            Assert.NotNull(sut);
        }
    }

    public class ReadAsync : AppendBlobDataStoreTests
    {
        [Fact]
        public async Task Should_return_null_when_blob_does_not_exist()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found", "BlobNotFound", null));

            var result = await sut.ReadAsync(objectDocument);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_events_and_skip_commit_markers()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            var ndjson = CreateNdjsonContent((1, "Test.Created"), (2, "Test.Updated"));
            var markerLine = CreateCommitMarkerLine("hash1", "*", 2);
            var fullContent = ndjson + markerLine + "\n";

            SetupDownloadStreamingToReturn(fullContent);

            var result = await sut.ReadAsync(objectDocument);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(2, eventsList.Count);
            Assert.Equal(1, eventsList[0].EventVersion);
            Assert.Equal(2, eventsList[1].EventVersion);
        }

        [Fact]
        public async Task Should_filter_by_start_version()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            var ndjson = CreateNdjsonContent((1, "Test.Created"), (2, "Test.Updated"), (3, "Test.Completed"));
            var markerLine = CreateCommitMarkerLine("hash1", "*", 3);
            var fullContent = ndjson + markerLine + "\n";

            SetupDownloadStreamingToReturn(fullContent);

            var result = await sut.ReadAsync(objectDocument, startVersion: 2);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(2, eventsList.Count);
            Assert.All(eventsList, e => Assert.True(e.EventVersion >= 2));
        }

        [Fact]
        public async Task Should_filter_by_until_version()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            var ndjson = CreateNdjsonContent((1, "Test.Created"), (2, "Test.Updated"), (3, "Test.Completed"));
            var markerLine = CreateCommitMarkerLine("hash1", "*", 3);
            var fullContent = ndjson + markerLine + "\n";

            SetupDownloadStreamingToReturn(fullContent);

            var result = await sut.ReadAsync(objectDocument, untilVersion: 2);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(2, eventsList.Count);
            Assert.All(eventsList, e => Assert.True(e.EventVersion <= 2));
        }

        [Fact]
        public async Task Should_cap_at_current_stream_version()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            // Object document says version is 2, but blob has orphaned events for v3
            objectDocument.Active.CurrentStreamVersion = 2;

            var ndjson = CreateNdjsonContent((1, "Test.Created"), (2, "Test.Updated"), (3, "Test.Orphaned"));
            var marker1 = CreateCommitMarkerLine("hash1", "*", 2);
            var marker2 = CreateCommitMarkerLine("hash-orphan", "hash1", 3);
            var fullContent = ndjson + marker1 + "\n" + marker2 + "\n";

            SetupDownloadStreamingToReturn(fullContent);

            var result = await sut.ReadAsync(objectDocument);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(2, eventsList.Count);
            Assert.DoesNotContain(eventsList, e => e.EventVersion == 3);
        }

        [Fact]
        public async Task Should_deduplicate_by_keeping_last_occurrence_per_version()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 2;

            // Simulate orphan scenario: version 1 appears twice
            var line1a = JsonSerializer.Serialize(new BlobJsonEvent
            {
                EventVersion = 1, EventType = "Test.FirstOccurrence", SchemaVersion = 1,
                Timestamp = DateTimeOffset.UtcNow, Payload = "{}"
            }, AppendBlobTestJsonContext.Default.BlobJsonEvent);
            var marker1 = CreateCommitMarkerLine("h1", "*", 1);
            var line1b = JsonSerializer.Serialize(new BlobJsonEvent
            {
                EventVersion = 1, EventType = "Test.SecondOccurrence", SchemaVersion = 1,
                Timestamp = DateTimeOffset.UtcNow, Payload = "{}"
            }, AppendBlobTestJsonContext.Default.BlobJsonEvent);
            var line2 = JsonSerializer.Serialize(new BlobJsonEvent
            {
                EventVersion = 2, EventType = "Test.Updated", SchemaVersion = 1,
                Timestamp = DateTimeOffset.UtcNow, Payload = "{}"
            }, AppendBlobTestJsonContext.Default.BlobJsonEvent);
            var marker2 = CreateCommitMarkerLine("h2", "h1", 2);

            var content = $"{line1a}\n{marker1}\n{line1b}\n{line2}\n{marker2}\n";
            SetupDownloadStreamingToReturn(content);

            var result = await sut.ReadAsync(objectDocument);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(2, eventsList.Count);
            // Last occurrence wins
            Assert.Equal("Test.SecondOccurrence", eventsList[0].EventType);
            Assert.Equal("Test.Updated", eventsList[1].EventType);
        }

        [Fact]
        public async Task Should_use_ndjson_extension()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found", "BlobNotFound", null));

            await sut.ReadAsync(objectDocument);

            containerClient.Received(1).GetAppendBlobClient("test-stream.ndjson");
        }
    }

    public class IncrementalRead : AppendBlobDataStoreTests
    {
        private static string CreateCommitMarkerLineWithOffset(string hash, string prevHash, int version, long offset)
        {
            var marker = new AppendBlobCommitMarker
            {
                Hash = hash,
                PrevHash = prevHash,
                Version = version,
                Offset = offset
            };
            return JsonSerializer.Serialize(marker, AppendBlobTestJsonContext.Default.AppendBlobCommitMarker);
        }

        private void SetupGetPropertiesToReturn(long contentLength)
        {
            var properties = BlobsModelFactory.BlobProperties(contentLength: contentLength);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);
        }

        [Fact]
        public async Task Should_use_incremental_read_when_markers_cover_start_version()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 10;

            // Blob is large enough for incremental reads (> 32KB)
            SetupGetPropertiesToReturn(50_000);

            // Build tail content: two markers that bracket startVersion=6
            // Marker at v5: batch v1-v5 starts at offset 0
            // Marker at v10: batch v6-v10 starts at offset 25000
            var tailMarker1 = CreateCommitMarkerLineWithOffset("h1", "*", 5, 0);
            var tailMarker2 = CreateCommitMarkerLineWithOffset("h2", "h1", 10, 25_000);
            var tailContent = tailMarker1 + "\n" + tailMarker2 + "\n";

            // Build partial content that would be returned from range-read at offset 25000
            var partialEvents = CreateNdjsonContent((6, "Test.V6"), (7, "Test.V7"), (8, "Test.V8"), (9, "Test.V9"), (10, "Test.V10"));
            var partialMarker = CreateCommitMarkerLineWithOffset("h2", "h1", 10, 25_000);
            var partialContent = partialEvents + partialMarker + "\n";

            // Mock DownloadStreamingAsync to return different content based on range
            var tailBytes = Encoding.UTF8.GetBytes(tailContent);
            var partialBytes = Encoding.UTF8.GetBytes(partialContent);

            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var options = callInfo.Arg<BlobDownloadOptions>();
                    byte[] bytes;
                    if (options?.Range.Offset == 25_000)
                    {
                        // Range read from offset 25000 (incremental data read)
                        bytes = partialBytes;
                    }
                    else
                    {
                        // Tail read
                        bytes = tailBytes;
                    }
                    var memStream = new MemoryStream(bytes);
                    var downloadResult = BlobsModelFactory.BlobDownloadStreamingResult(content: memStream);
                    return Response.FromValue(downloadResult, Substitute.For<Response>());
                });

            var result = await sut.ReadAsync(objectDocument, startVersion: 6);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(5, eventsList.Count);
            Assert.Equal(6, eventsList[0].EventVersion);
            Assert.Equal(10, eventsList[4].EventVersion);
        }

        [Fact]
        public async Task Should_fall_back_to_full_download_when_blob_is_small()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 3;

            // Blob is small (< 32KB threshold)
            SetupGetPropertiesToReturn(1_000);

            // The full download will be used
            var fullContent = CreateNdjsonContent((1, "Test.V1"), (2, "Test.V2"), (3, "Test.V3"));
            var marker = CreateCommitMarkerLine("h1", "*", 3);
            var fullContentWithMarker = fullContent + marker + "\n";

            var fullBytes = Encoding.UTF8.GetBytes(fullContentWithMarker);
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var memStream = new MemoryStream(fullBytes);
                    var downloadResult = BlobsModelFactory.BlobDownloadStreamingResult(content: memStream);
                    return Response.FromValue(downloadResult, Substitute.For<Response>());
                });

            var result = await sut.ReadAsync(objectDocument, startVersion: 2);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(2, eventsList.Count);
            Assert.Equal(2, eventsList[0].EventVersion);
            Assert.Equal(3, eventsList[1].EventVersion);
        }

        [Fact]
        public async Task Should_fall_back_when_markers_lack_offset()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 5;

            SetupGetPropertiesToReturn(50_000);

            // Markers without offset (old format, before offset feature)
            var tailMarker1 = CreateCommitMarkerLine("h1", "*", 3);
            var tailMarker2 = CreateCommitMarkerLine("h2", "h1", 5);
            var tailContent = tailMarker1 + "\n" + tailMarker2 + "\n";

            // Full download content (fallback)
            var fullContent = CreateNdjsonContent((1, "Test.V1"), (2, "Test.V2"), (3, "Test.V3"), (4, "Test.V4"), (5, "Test.V5"));
            var marker = CreateCommitMarkerLine("h2", "h1", 5);
            var fullContentWithMarker = fullContent + marker + "\n";

            var tailBytes = Encoding.UTF8.GetBytes(tailContent);
            var fullBytes = Encoding.UTF8.GetBytes(fullContentWithMarker);

            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var options = callInfo.Arg<BlobDownloadOptions>();
                    // If range has offset near end of blob → tail read; otherwise → full download
                    byte[] bytes = (options?.Range.Offset > 40_000) ? tailBytes : fullBytes;
                    var memStream = new MemoryStream(bytes);
                    var downloadResult = BlobsModelFactory.BlobDownloadStreamingResult(content: memStream);
                    return Response.FromValue(downloadResult, Substitute.For<Response>());
                });

            var result = await sut.ReadAsync(objectDocument, startVersion: 4);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(2, eventsList.Count);
            Assert.Equal(4, eventsList[0].EventVersion);
        }

        [Fact]
        public async Task Should_fall_back_when_start_version_is_too_old_for_tail_markers()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 100;

            SetupGetPropertiesToReturn(50_000);

            // Tail only has recent markers (v90 and v100)
            var tailMarker1 = CreateCommitMarkerLineWithOffset("h90", "h80", 90, 40_000);
            var tailMarker2 = CreateCommitMarkerLineWithOffset("h100", "h90", 100, 45_000);
            var tailContent = tailMarker1 + "\n" + tailMarker2 + "\n";

            // Full download content
            var fullEvents = CreateNdjsonContent((1, "Test.V1"), (50, "Test.V50"), (100, "Test.V100"));
            var fullMarker = CreateCommitMarkerLine("h100", "h90", 100);
            var fullContentWithMarker = fullEvents + fullMarker + "\n";

            var tailBytes = Encoding.UTF8.GetBytes(tailContent);
            var fullBytes = Encoding.UTF8.GetBytes(fullContentWithMarker);

            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var options = callInfo.Arg<BlobDownloadOptions>();
                    byte[] bytes = (options?.Range.Offset > 40_000) ? tailBytes : fullBytes;
                    var memStream = new MemoryStream(bytes);
                    var downloadResult = BlobsModelFactory.BlobDownloadStreamingResult(content: memStream);
                    return Response.FromValue(downloadResult, Substitute.For<Response>());
                });

            // startVersion=5 is before the earliest tail marker (v90), can't determine offset → fallback
            // Falls back to full download, then filters to startVersion=5..100 → v50 and v100
            var result = await sut.ReadAsync(objectDocument, startVersion: 5);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(2, eventsList.Count);
            Assert.Equal(50, eventsList[0].EventVersion);
            Assert.Equal(100, eventsList[1].EventVersion);
        }

        [Fact]
        public async Task Should_return_empty_when_start_version_beyond_last_marker()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 5;

            SetupGetPropertiesToReturn(50_000);

            var tailMarker1 = CreateCommitMarkerLineWithOffset("h1", "*", 3, 0);
            var tailMarker2 = CreateCommitMarkerLineWithOffset("h2", "h1", 5, 500);
            var tailContent = tailMarker1 + "\n" + tailMarker2 + "\n";

            var tailBytes = Encoding.UTF8.GetBytes(tailContent);
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var memStream = new MemoryStream(tailBytes);
                    var downloadResult = BlobsModelFactory.BlobDownloadStreamingResult(content: memStream);
                    return Response.FromValue(downloadResult, Substitute.For<Response>());
                });

            // startVersion=10 is beyond last marker (v=5) → empty result
            var result = await sut.ReadAsync(objectDocument, startVersion: 10);

            Assert.NotNull(result);
            Assert.Empty(result!);
        }
    }

    public class ReadAsStreamAsync : AppendBlobDataStoreTests
    {
        [Fact]
        public async Task Should_yield_no_events_when_blob_does_not_exist()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found", "BlobNotFound", null));

            var eventsList = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(objectDocument))
            {
                eventsList.Add(evt);
            }

            Assert.Empty(eventsList);
        }

        [Fact]
        public async Task Should_yield_events_and_skip_markers()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            var ndjson = CreateNdjsonContent((1, "Test.Created"), (2, "Test.Updated"));
            var markerLine = CreateCommitMarkerLine("hash1", "*", 2);
            var fullContent = ndjson + markerLine + "\n";

            SetupDownloadStreamingToReturn(fullContent);

            var eventsList = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(objectDocument))
            {
                eventsList.Add(evt);
            }

            Assert.Equal(2, eventsList.Count);
        }

        [Fact]
        public async Task Should_respect_cancellation_token()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            var cts = new CancellationTokenSource();

            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .Returns<Response<BlobDownloadStreamingResult>>(callInfo =>
                {
                    callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Should not reach here");
                });

            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in sut.ReadAsStreamAsync(objectDocument, cancellationToken: cts.Token))
                {
                }
            });
        }
    }

    public class AppendAsync : AppendBlobDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AppendAsync(null!, default, events));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_no_events_provided()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            await Assert.ThrowsAsync<ArgumentException>(() => sut.AppendAsync(objectDocument, default, Array.Empty<IEvent>()));
        }

        [Fact]
        public async Task Should_append_events_and_commit_marker_when_blob_exists()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            // Setup GetPropertiesAsync - blob exists with some content
            var properties = BlobsModelFactory.BlobProperties(contentLength: 100);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            // Setup range-read to return a matching commit marker
            var markerLine = CreateCommitMarkerLine("hash-prev", "*", 0);
            SetupDownloadStreamingToReturn(markerLine + "\n");

            // Setup AppendBlockAsync
            var appendResponse = Substitute.For<Response<BlobAppendInfo>>();
            appendBlobClient.AppendBlockAsync(Arg.Any<Stream>(), Arg.Any<AppendBlobAppendBlockOptions>(), Arg.Any<CancellationToken>())
                .Returns(appendResponse);

            await sut.AppendAsync(objectDocument, default, events);

            // Verify AppendBlockAsync was called with IfAppendPositionEqual condition
            await appendBlobClient.Received(1).AppendBlockAsync(
                Arg.Any<Stream>(),
                Arg.Is<AppendBlobAppendBlockOptions>(o =>
                    o.Conditions != null && o.Conditions.IfAppendPositionEqual == 100),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_concurrency_error_when_marker_hash_mismatch_from_concurrent_writer()
        {
            // Simulate: Writer B read doc at v5 (after Writer A's Phase 1 set it to v5),
            // B's Phase 1 bumps to v8, but marker is still at v2 from an earlier commit.
            // marker.v(2) != minEvent-1(7) and marker.v(2) < maxEvent(8) → concurrent writer error.
            var sut = new AppendBlobDataStore(clientFactory, false);

            // Events start at v8 (Writer B's events after reading doc at v7)
            var concurrentEvents = new IEvent[] { CreateTestEvent(8) };

            // Setup GetPropertiesAsync
            var properties = BlobsModelFactory.BlobProperties(contentLength: 100);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            // Marker at v2 — doesn't match the expected base version (7)
            var markerLine = CreateCommitMarkerLine("WRONG-HASH", "*", 2);
            SetupDownloadStreamingToReturn(markerLine + "\n");

            await Assert.ThrowsAsync<BlobDataStoreProcessingException>(
                () => sut.AppendAsync(objectDocument, default, concurrentEvents));
        }

        [Fact]
        public async Task Should_write_repair_marker_when_orphan_detected()
        {
            // Simulate: Phase 2 timeout wrote events+marker server-side, recovery rolled back doc.
            // Retry produces same events. marker.v(1) >= maxEvent(1) → orphan recovery.
            var sut = new AppendBlobDataStore(clientFactory, false);

            var properties = BlobsModelFactory.BlobProperties(contentLength: 200);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            // Orphaned marker with version covering our event (v1), but hash doesn't match doc.PrevHash
            var markerLine = CreateCommitMarkerLine("orphan-hash", "*", 1);
            SetupDownloadStreamingToReturn(markerLine + "\n");

            var appendResponse = Substitute.For<Response<BlobAppendInfo>>();
            appendBlobClient.AppendBlockAsync(Arg.Any<Stream>(), Arg.Any<AppendBlobAppendBlockOptions>(), Arg.Any<CancellationToken>())
                .Returns(appendResponse);

            // Should NOT throw — orphan recovery writes repair marker and returns
            await sut.AppendAsync(objectDocument, default, events);

            // Verify a single AppendBlock was called (repair marker only, no events)
            await appendBlobClient.Received(1).AppendBlockAsync(
                Arg.Any<Stream>(),
                Arg.Is<AppendBlobAppendBlockOptions>(o =>
                    o.Conditions != null && o.Conditions.IfAppendPositionEqual == 200),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_proceed_with_adjusted_prev_hash_on_hash_drift()
        {
            // Simulate: Phase 2 failed cleanly (events not written), recovery re-saved doc
            // changing its hash. marker.v(0) == minEvent-1(0) → hash drift, safe to proceed.
            var sut = new AppendBlobDataStore(clientFactory, false);

            var properties = BlobsModelFactory.BlobProperties(contentLength: 100);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            // Marker at v0 with hash that doesn't match doc.PrevHash (hash changed from recovery)
            var markerLine = CreateCommitMarkerLine("drifted-hash", "*", 0);
            SetupDownloadStreamingToReturn(markerLine + "\n");

            var appendResponse = Substitute.For<Response<BlobAppendInfo>>();
            appendBlobClient.AppendBlockAsync(Arg.Any<Stream>(), Arg.Any<AppendBlobAppendBlockOptions>(), Arg.Any<CancellationToken>())
                .Returns(appendResponse);

            // Should NOT throw — proceeds with adjusted PrevHash
            await sut.AppendAsync(objectDocument, default, events);

            // Verify AppendBlock was called (events + marker written)
            await appendBlobClient.Received(1).AppendBlockAsync(
                Arg.Any<Stream>(),
                Arg.Is<AppendBlobAppendBlockOptions>(o =>
                    o.Conditions != null && o.Conditions.IfAppendPositionEqual == 100),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_create_initial_blob_when_blob_does_not_exist()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            // First call: blob doesn't exist
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(
                    _ => throw new RequestFailedException(404, "Not found", "BlobNotFound", null),
                    _ => Response.FromValue(BlobsModelFactory.BlobProperties(contentLength: 50), Substitute.For<Response>()));

            // Setup CreateIfNotExistsAsync
            appendBlobClient.CreateIfNotExistsAsync(Arg.Any<AppendBlobCreateOptions>(), Arg.Any<CancellationToken>())
                .Returns(Substitute.For<Response<BlobContentInfo>>());

            // Setup AppendBlockAsync for initial marker and for events
            var appendResponse = Substitute.For<Response<BlobAppendInfo>>();
            appendBlobClient.AppendBlockAsync(Arg.Any<Stream>(), Arg.Any<AppendBlobAppendBlockOptions>(), Arg.Any<CancellationToken>())
                .Returns(appendResponse);

            // Setup range-read for the newly created blob with initial marker
            var initialMarker = CreateCommitMarkerLine("hash-abc", "*", 0);
            SetupDownloadStreamingToReturn(initialMarker + "\n");

            // PrevHash matches the initial marker hash
            objectDocument.PrevHash.Returns("hash-abc");

            await sut.AppendAsync(objectDocument, default, events);

            // Should have created the blob
            await appendBlobClient.Received(1).CreateIfNotExistsAsync(
                Arg.Any<AppendBlobCreateOptions>(), Arg.Any<CancellationToken>());
        }
    }

    public class RemoveEventsForFailedCommitAsync : AppendBlobDataStoreTests
    {
        [Fact]
        public async Task Should_return_zero_because_append_blobs_cannot_remove_data()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            var result = await sut.RemoveEventsForFailedCommitAsync(objectDocument, 1, 5);

            Assert.Equal(0, result);
        }
    }

    public class CreateBlobClient : AppendBlobDataStoreTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.ObjectName.Returns((string?)null);

            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReadAsync(objectDocument));
        }

        [Fact]
        public async Task Should_create_container_when_auto_create_is_enabled()
        {
            var sut = new AppendBlobDataStore(clientFactory, true);
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found", "BlobNotFound", null));

            AppendBlobDataStore.ClearVerifiedContainersCache();

            await sut.ReadAsync(objectDocument);

            await containerClient.Received(1).CreateIfNotExistsAsync();
        }

        [Fact]
        public async Task Should_not_create_container_when_auto_create_is_disabled()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found", "BlobNotFound", null));

            await sut.ReadAsync(objectDocument);

            await containerClient.DidNotReceive().CreateIfNotExistsAsync();
        }

        [Fact]
        public async Task Should_use_lowercase_object_name_for_container()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.ObjectName.Returns("TestObject");
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found", "BlobNotFound", null));

            await sut.ReadAsync(objectDocument);

            blobServiceClient.Received(1).GetBlobContainerClient("testobject");
        }
    }

    public class ClearVerifiedContainersCacheTest
    {
        [Fact]
        public async Task ClearVerifiedContainersCache_should_allow_container_creation_again()
        {
            // Standalone test with isolated mocks and a unique container name
            // to avoid interference from parallel test classes clearing the static cache.
            var clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var blobServiceClient = Substitute.For<BlobServiceClient>();
            var containerClient = Substitute.For<BlobContainerClient>();
            var appendBlobClient = Substitute.For<AppendBlobClient>();
            var objectDocument = Substitute.For<IObjectDocument>();
            var streamInfo = Substitute.For<StreamInformation>();

            streamInfo.StreamIdentifier = "cache-test-stream";
            streamInfo.DataStore = "cache-conn";
            streamInfo.CurrentStreamVersion = 10;
            streamInfo.ChunkSettings = new StreamChunkSettings { EnableChunks = false };
            objectDocument.Active.Returns(streamInfo);
            objectDocument.ObjectName.Returns("CacheIsolatedObj");
            objectDocument.ObjectId.Returns("cache-id");
            objectDocument.TerminatedStreams.Returns([]);

            clientFactory.CreateClient("cache-conn").Returns(blobServiceClient);
            blobServiceClient.GetBlobContainerClient("cacheisolatedobj").Returns(containerClient);
            containerClient.GetAppendBlobClient(Arg.Any<string>()).Returns(appendBlobClient);
            appendBlobClient.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new RequestFailedException(404, "Not found", "BlobNotFound", null));

            var sut = new AppendBlobDataStore(clientFactory, true);

            // Clear cache, reset mock, then read.
            // Because we use a unique container name ("cacheisolatedobj"), no parallel test
            // will TryAdd this key — only ClearVerifiedContainersCache can remove it.
            // We do clear + reset + read atomically enough that the assertion is reliable.
            AppendBlobDataStore.ClearVerifiedContainersCache();
            containerClient.ClearReceivedCalls();
            await sut.ReadAsync(objectDocument);

            // After clearing cache, container should be (re-)created
            await containerClient.Received(1).CreateIfNotExistsAsync();
        }
    }

    public class StreamClosedDetection : AppendBlobDataStoreTests
    {
        public StreamClosedDetection()
        {
            AppendBlobDataStore.ClearClosedStreamCache();
            // Use a unique stream identifier to avoid polluting the static ClosedStreams
            // cache with "test-stream" which is used by other parallel test classes.
            objectDocument.Active.StreamIdentifier = "closed-detection-stream";
        }

        private static string CreateCommitMarkerLineWithClosed(string hash, string prevHash, int version, bool closed)
        {
            var marker = new AppendBlobCommitMarker
            {
                Hash = hash,
                PrevHash = prevHash,
                Version = version,
                Closed = closed
            };
            return JsonSerializer.Serialize(marker, AppendBlobTestJsonContext.Default.AppendBlobCommitMarker);
        }

        [Fact]
        public async Task Should_detect_closed_stream_via_marker_closed_field()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            var properties = BlobsModelFactory.BlobProperties(contentLength: 100);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            // Marker with Closed = true (new format)
            var markerLine = CreateCommitMarkerLineWithClosed("hash-prev", "*", 5, closed: true);
            SetupDownloadStreamingToReturn(markerLine + "\n");

            await Assert.ThrowsAsync<EventStreamClosedException>(
                () => sut.AppendAsync(objectDocument, default, events));
        }

        [Fact]
        public async Task Should_detect_closed_stream_via_legacy_string_matching()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            var properties = BlobsModelFactory.BlobProperties(contentLength: 200);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            // Old-format marker (no Closed field) + a close event line
            var markerLine = CreateCommitMarkerLine("hash-prev", "*", 5);
            var closedEvent = JsonSerializer.Serialize(new BlobJsonEvent
            {
                EventVersion = 5,
                EventType = "EventStream.Closed",
                SchemaVersion = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Payload = "{}"
            }, AppendBlobTestJsonContext.Default.BlobJsonEvent);
            SetupDownloadStreamingToReturn(closedEvent + "\n" + markerLine + "\n");

            await Assert.ThrowsAsync<EventStreamClosedException>(
                () => sut.AppendAsync(objectDocument, default, events));
        }

        [Fact]
        public async Task Should_use_closed_stream_cache_on_subsequent_calls()
        {
            // Use fully isolated mocks and a unique stream ID to avoid
            // cache interference from parallel test classes (same pattern as ClearVerifiedContainersCacheTest).
            var isolatedFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            var isolatedBlobService = Substitute.For<BlobServiceClient>();
            var isolatedContainer = Substitute.For<BlobContainerClient>();
            var isolatedAppendBlob = Substitute.For<AppendBlobClient>();
            var isolatedDoc = Substitute.For<IObjectDocument>();
            var isolatedStream = Substitute.For<StreamInformation>();

            isolatedStream.StreamIdentifier = "cache-isolated-closed-stream";
            isolatedStream.DataStore = "cache-conn";
            isolatedStream.CurrentStreamVersion = 10;
            isolatedStream.ChunkSettings = new StreamChunkSettings { EnableChunks = false };
            isolatedDoc.Active.Returns(isolatedStream);
            isolatedDoc.ObjectName.Returns("CacheClosedObj");
            isolatedDoc.ObjectId.Returns("cache-closed-id");
            isolatedDoc.Hash.Returns("hash-abc");
            isolatedDoc.PrevHash.Returns("hash-prev");
            isolatedDoc.TerminatedStreams.Returns([]);

            isolatedFactory.CreateClient("cache-conn").Returns(isolatedBlobService);
            isolatedBlobService.GetBlobContainerClient("cacheclosedobj").Returns(isolatedContainer);
            isolatedContainer.GetAppendBlobClient(Arg.Any<string>()).Returns(isolatedAppendBlob);

            var properties = BlobsModelFactory.BlobProperties(contentLength: 100);
            isolatedAppendBlob.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(Response.FromValue(properties, Substitute.For<Response>()));

            var markerLine = CreateCommitMarkerLineWithClosed("hash-prev", "*", 5, closed: true);
            var markerBytes = Encoding.UTF8.GetBytes(markerLine + "\n");
            isolatedAppendBlob.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var ms = new MemoryStream(markerBytes);
                    return Response.FromValue(BlobsModelFactory.BlobDownloadStreamingResult(content: ms), Substitute.For<Response>());
                });

            var sut = new AppendBlobDataStore(isolatedFactory, false);
            var testEvents = new IEvent[] { CreateTestEvent(1) };

            AppendBlobDataStore.ClearClosedStreamCache();

            // First call: detects closed via marker, adds to cache
            await Assert.ThrowsAsync<EventStreamClosedException>(
                () => sut.AppendAsync(isolatedDoc, default, testEvents));

            // Second call: should throw from cache before any I/O
            isolatedAppendBlob.ClearReceivedCalls();
            await Assert.ThrowsAsync<EventStreamClosedException>(
                () => sut.AppendAsync(isolatedDoc, default, testEvents));

            // No I/O should have occurred because the cache check is before GetProperties
            await isolatedAppendBlob.DidNotReceive().GetPropertiesAsync(
                Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_set_closed_on_marker_when_batch_contains_close_event()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            var closeEvent = CreateTestEvent(1, "EventStream.Closed");
            var properties = BlobsModelFactory.BlobProperties(contentLength: 100);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            // Existing marker matches doc hash
            var markerLine = CreateCommitMarkerLine("hash-prev", "*", 0);
            SetupDownloadStreamingToReturn(markerLine + "\n");

            Stream? capturedStream = null;
            var appendResponse = Substitute.For<Response<BlobAppendInfo>>();
            appendBlobClient.AppendBlockAsync(Arg.Any<Stream>(), Arg.Any<AppendBlobAppendBlockOptions>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var stream = callInfo.Arg<Stream>();
                    capturedStream = new MemoryStream();
                    stream.CopyTo(capturedStream);
                    capturedStream.Position = 0;
                    return appendResponse;
                });

            await sut.AppendAsync(objectDocument, default, new IEvent[] { closeEvent });

            // Parse the appended NDJSON to find the commit marker and verify Closed = true
            Assert.NotNull(capturedStream);
            var content = new StreamReader(capturedStream!).ReadToEnd();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var markerJson = lines.First(l => l.StartsWith("{\"$m\":"));
            var marker = JsonSerializer.Deserialize(markerJson, AppendBlobTestJsonContext.Default.AppendBlobCommitMarker);
            Assert.NotNull(marker);
            Assert.True(marker!.Closed);
        }
    }

    public class SnapshotIntegration : AppendBlobDataStoreTests
    {
        [Fact]
        public async Task Should_return_events_from_snapshot_version_onward()
        {
            // Simulates loading after a snapshot at version 5: ReadAsync(startVersion: 6)
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 10;

            var ndjson = CreateNdjsonContent(
                (1, "Test.V1"), (2, "Test.V2"), (3, "Test.V3"),
                (4, "Test.V4"), (5, "Test.V5"), (6, "Test.V6"),
                (7, "Test.V7"), (8, "Test.V8"), (9, "Test.V9"), (10, "Test.V10"));
            var markerLine = CreateCommitMarkerLine("h1", "*", 10);
            var fullContent = ndjson + markerLine + "\n";

            SetupDownloadStreamingToReturn(fullContent);

            var result = await sut.ReadAsync(objectDocument, startVersion: 6);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Equal(5, eventsList.Count);
            Assert.Equal(6, eventsList[0].EventVersion);
            Assert.Equal(10, eventsList[4].EventVersion);
        }

        [Fact]
        public async Task Should_return_only_last_event_when_snapshot_at_penultimate_version()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 5;

            var ndjson = CreateNdjsonContent(
                (1, "Test.V1"), (2, "Test.V2"), (3, "Test.V3"),
                (4, "Test.V4"), (5, "Test.V5"));
            var markerLine = CreateCommitMarkerLine("h1", "*", 5);
            var fullContent = ndjson + markerLine + "\n";

            SetupDownloadStreamingToReturn(fullContent);

            // Snapshot at v4 → startVersion = 5
            var result = await sut.ReadAsync(objectDocument, startVersion: 5);

            Assert.NotNull(result);
            var eventsList = result!.ToList();
            Assert.Single(eventsList);
            Assert.Equal(5, eventsList[0].EventVersion);
        }

        [Fact]
        public async Task Should_return_correct_subset_via_streaming_with_snapshot()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            objectDocument.Active.CurrentStreamVersion = 5;

            var ndjson = CreateNdjsonContent(
                (1, "Test.V1"), (2, "Test.V2"), (3, "Test.V3"),
                (4, "Test.V4"), (5, "Test.V5"));
            var markerLine = CreateCommitMarkerLine("h1", "*", 5);
            var fullContent = ndjson + markerLine + "\n";

            SetupDownloadStreamingToReturn(fullContent);

            var eventsList = new List<IEvent>();
            await foreach (var evt in sut.ReadAsStreamAsync(objectDocument, startVersion: 3))
            {
                eventsList.Add(evt);
            }

            Assert.Equal(3, eventsList.Count);
            Assert.Equal(3, eventsList[0].EventVersion);
            Assert.Equal(5, eventsList[2].EventVersion);
        }
    }

    public class ChunkingTests : AppendBlobDataStoreTests
    {
        private static IObjectDocument CreateChunkedDocument(string streamId, int lastChunkId)
        {
            var doc = Substitute.For<IObjectDocument>();
            var streamInfo = Substitute.For<StreamInformation>();
            streamInfo.StreamIdentifier = streamId;
            streamInfo.DataStore = "test-connection";
            streamInfo.CurrentStreamVersion = 10;
            streamInfo.ChunkSettings = new StreamChunkSettings { EnableChunks = true, ChunkSize = 1000 };
            streamInfo.StreamChunks =
            [
                new StreamChunk(0, 0, 999),
                new StreamChunk(lastChunkId, 1000, -1)
            ];

            doc.Active.Returns(streamInfo);
            doc.ObjectName.Returns("TestObject");
            doc.ObjectId.Returns("test-id");
            return doc;
        }

        [Fact]
        public void GetDocumentPathForRead_should_return_plain_path_when_chunking_disabled()
        {
            var path = AppendBlobDataStore.GetDocumentPathForRead(objectDocument);
            Assert.Equal("test-stream.ndjson", path);
        }

        [Fact]
        public void GetDocumentPathForRead_should_return_last_chunk_path_when_no_specific_chunk()
        {
            var doc = CreateChunkedDocument("stream-abc", lastChunkId: 1);
            var path = AppendBlobDataStore.GetDocumentPathForRead(doc);
            Assert.Equal("stream-abc-0000000001.ndjson", path);
        }

        [Fact]
        public void GetDocumentPathForRead_should_return_specific_chunk_path()
        {
            var doc = CreateChunkedDocument("stream-abc", lastChunkId: 1);
            var path = AppendBlobDataStore.GetDocumentPathForRead(doc, chunk: 0);
            Assert.Equal("stream-abc-0000000000.ndjson", path);
        }

        [Fact]
        public void GetDocumentPathForAppend_should_return_plain_path_when_chunking_disabled()
        {
            var path = AppendBlobDataStore.GetDocumentPathForAppend(objectDocument);
            Assert.Equal("test-stream.ndjson", path);
        }

        [Fact]
        public void GetDocumentPathForAppend_should_return_last_chunk_path_when_chunking_enabled()
        {
            var doc = CreateChunkedDocument("stream-abc", lastChunkId: 2);
            var path = AppendBlobDataStore.GetDocumentPathForAppend(doc);
            Assert.Equal("stream-abc-0000000002.ndjson", path);
        }
    }

    public class BlockLimitTests : AppendBlobDataStoreTests
    {
        [Fact]
        public async Task Should_throw_EventStreamClosedException_when_block_count_at_threshold()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);

            // Use a stream identifier with proper format for ComputeContinuationStreamId
            objectDocument.Active.StreamIdentifier = "testid-0000000000";
            objectDocument.Active.DataStore = "test-connection";
            objectDocument.Active.DocumentStore = "test-doc-store";

            var properties = BlobsModelFactory.BlobProperties(
                contentLength: 100,
                blobCommittedBlockCount: AppendBlobDataStore.BlockCountThreshold);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            var ex = await Assert.ThrowsAsync<EventStreamClosedException>(
                () => sut.AppendAsync(objectDocument, default, events));

            Assert.True(ex.HasContinuation);
            Assert.Equal("testid-0000000000", ex.StreamIdentifier);
            Assert.Equal("testid-0000000001", ex.ContinuationStreamId);
            Assert.Equal("appendblob", ex.ContinuationStreamType);
            Assert.Equal("test-connection", ex.ContinuationDataStore);
            Assert.Equal("test-doc-store", ex.ContinuationDocumentStore);
            Assert.Contains("50,000 block limit", ex.Reason);
        }

        [Fact]
        public async Task Should_include_correct_continuation_stream_id()
        {
            var sut = new AppendBlobDataStore(clientFactory, false);
            AppendBlobDataStore.ClearClosedStreamCache();

            // Stream identifier format: {objectId}-{10-digit-suffix}
            objectDocument.Active.StreamIdentifier = "abc123-0000000000";
            objectDocument.Active.DataStore = "test-connection";
            objectDocument.Active.DocumentStore = "test-doc-store";

            var properties = BlobsModelFactory.BlobProperties(
                contentLength: 100,
                blobCommittedBlockCount: AppendBlobDataStore.BlockCountThreshold);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            var ex = await Assert.ThrowsAsync<EventStreamClosedException>(
                () => sut.AppendAsync(objectDocument, default, events));

            Assert.Equal("abc123-0000000001", ex.ContinuationStreamId);
        }

        [Fact]
        public void ComputeContinuationStreamId_should_increment_suffix()
        {
            Assert.Equal("abc-0000000001", AppendBlobDataStore.ComputeContinuationStreamId("abc-0000000000"));
            Assert.Equal("abc-0000000100", AppendBlobDataStore.ComputeContinuationStreamId("abc-0000000099"));
            Assert.Equal("prefix-0000000002", AppendBlobDataStore.ComputeContinuationStreamId("prefix-0000000001"));
        }

        [Fact]
        public void ComputeContinuationStreamId_should_handle_multi_dash_prefix()
        {
            Assert.Equal("a-b-c-0000000001", AppendBlobDataStore.ComputeContinuationStreamId("a-b-c-0000000000"));
        }

        [Fact]
        public void ComputeContinuationStreamId_should_throw_on_invalid_format()
        {
            Assert.Throws<InvalidOperationException>(() => AppendBlobDataStore.ComputeContinuationStreamId("nodash"));
            Assert.Throws<InvalidOperationException>(() => AppendBlobDataStore.ComputeContinuationStreamId("abc-notanumber"));
        }
    }

    public class CommitMarkerTests : AppendBlobDataStoreTests
    {
        [Fact]
        public async Task Sequential_appends_should_produce_correct_marker_chain()
        {
            // This test verifies that the append method includes the correct hash values
            // in the commit marker based on the document hash chain.
            var sut = new AppendBlobDataStore(clientFactory, false);

            var properties = BlobsModelFactory.BlobProperties(contentLength: 100);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            // Setup range-read with matching hash
            var markerLine = CreateCommitMarkerLine("hash-prev", "*", 0);
            SetupDownloadStreamingToReturn(markerLine + "\n");

            var appendResponse = Substitute.For<Response<BlobAppendInfo>>();
            appendBlobClient.AppendBlockAsync(Arg.Any<Stream>(), Arg.Any<AppendBlobAppendBlockOptions>(), Arg.Any<CancellationToken>())
                .Returns(appendResponse);

            await sut.AppendAsync(objectDocument, default, events);

            // Verify AppendBlockAsync was called
            await appendBlobClient.Received(1).AppendBlockAsync(
                Arg.Any<Stream>(),
                Arg.Any<AppendBlobAppendBlockOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_find_last_marker_in_small_blob()
        {
            // For blobs smaller than 4KB, the range-read covers the entire content
            var sut = new AppendBlobDataStore(clientFactory, false);

            var properties = BlobsModelFactory.BlobProperties(contentLength: 50);
            var propertiesResponse = Response.FromValue(properties, Substitute.For<Response>());
            appendBlobClient.GetPropertiesAsync(Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>())
                .Returns(propertiesResponse);

            var markerLine = CreateCommitMarkerLine("hash-prev", "*", 0);
            SetupDownloadStreamingToReturn(markerLine + "\n");

            var appendResponse = Substitute.For<Response<BlobAppendInfo>>();
            appendBlobClient.AppendBlockAsync(Arg.Any<Stream>(), Arg.Any<AppendBlobAppendBlockOptions>(), Arg.Any<CancellationToken>())
                .Returns(appendResponse);

            // Should not throw - small blob is handled correctly
            await sut.AppendAsync(objectDocument, default, events);

            // Verify the range read was done with correct range for small blob
            await appendBlobClient.Received(1).DownloadStreamingAsync(
                Arg.Is<BlobDownloadOptions>(o => o.Range.Offset == 0 && o.Range.Length == 50),
                Arg.Any<CancellationToken>());
        }
    }
}
