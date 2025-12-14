#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

/// <summary>
/// Tests for verifying checkpoint accumulation behavior in BlobProjectionFactory.
/// These tests ensure that when multiple version tokens are applied across saves,
/// all version tokens are preserved in the checkpoint.
/// </summary>
public partial class BlobProjectionFactoryCheckpointTests
{
    /// <summary>
    /// Test projection that tracks multiple objects.
    /// </summary>
    [JsonSerializable(typeof(TestProjectionWithExternalCheckpoint))]
    [JsonSerializable(typeof(Checkpoint))]
    [JsonSerializable(typeof(ObjectIdentifier))]
    [JsonSerializable(typeof(VersionIdentifier))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class TestProjectionJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Test projection with external checkpoint enabled.
    /// </summary>
    public class TestProjectionWithExternalCheckpoint : Projection
    {
        private Checkpoint _checkpoint = [];

        [JsonPropertyName("items")]
        public Dictionary<string, string> Items { get; set; } = [];

        [JsonIgnore] // External checkpoint - not serialized in main JSON
        public override Checkpoint Checkpoint
        {
            get => _checkpoint;
            set => _checkpoint = value;
        }

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? context = null) where T : class
        {
            // Simple fold - just track that we processed the event
            Items[versionToken.ObjectId] = @event.EventType;
            return Task.CompletedTask;
        }

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } = [];

        public override string ToJson()
        {
            return JsonSerializer.Serialize(this, TestProjectionJsonContext.Default.TestProjectionWithExternalCheckpoint);
        }

        public static TestProjectionWithExternalCheckpoint? LoadFromJson(string json)
        {
            return JsonSerializer.Deserialize(json, TestProjectionJsonContext.Default.TestProjectionWithExternalCheckpoint);
        }
    }

    /// <summary>
    /// Concrete factory for testing.
    /// </summary>
    public class TestProjectionFactory : BlobProjectionFactory<TestProjectionWithExternalCheckpoint>
    {
        public TestProjectionFactory(
            IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
            string connectionName,
            string containerOrPath)
            : base(blobServiceClientFactory, connectionName, containerOrPath)
        {
        }

        protected override bool HasExternalCheckpoint => true;

        protected override TestProjectionWithExternalCheckpoint New() => new();

        protected override TestProjectionWithExternalCheckpoint? LoadFromJson(
            string json,
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory)
        {
            return TestProjectionWithExternalCheckpoint.LoadFromJson(json);
        }
    }

    public class ExternalCheckpointAccumulation
    {
        private readonly IAzureClientFactory<BlobServiceClient> _clientFactory;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobContainerClient _containerClient;
        private readonly Dictionary<string, byte[]> _blobStorage;

        public ExternalCheckpointAccumulation()
        {
            _blobStorage = new Dictionary<string, byte[]>();
            _clientFactory = Substitute.For<IAzureClientFactory<BlobServiceClient>>();
            _blobServiceClient = Substitute.For<BlobServiceClient>();
            _containerClient = Substitute.For<BlobContainerClient>();

            _clientFactory.CreateClient("test-connection").Returns(_blobServiceClient);
            _blobServiceClient.GetBlobContainerClient("test-container").Returns(_containerClient);
            _containerClient.CreateIfNotExistsAsync(default, default, default).Returns(Task.FromResult(Substitute.For<Response<BlobContainerInfo>>()));
        }

        private void SetupBlobClient(string blobName)
        {
            var blobClient = Substitute.For<BlobClient>();
            _containerClient.GetBlobClient(blobName).Returns(blobClient);

            blobClient.ExistsAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo => Task.FromResult(Response.FromValue(_blobStorage.ContainsKey(blobName), Substitute.For<Response>())));

            blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    if (_blobStorage.TryGetValue(blobName, out var data))
                    {
                        var content = new BinaryData(data);
                        var downloadResult = BlobsModelFactory.BlobDownloadResult(content);
                        return Task.FromResult(Response.FromValue(downloadResult, Substitute.For<Response>()));
                    }
                    throw new RequestFailedException(404, "Blob not found");
                });

            blobClient.UploadAsync(Arg.Any<BinaryData>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var data = callInfo.Arg<BinaryData>();
                    _blobStorage[blobName] = data.ToArray();
                    return Task.FromResult(Substitute.For<Response<BlobContentInfo>>());
                });
        }

        [Fact]
        public async Task Should_accumulate_all_version_tokens_across_multiple_saves()
        {
            // Arrange
            var factory = new TestProjectionFactory(_clientFactory, "test-connection", "test-container");
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            // Setup blob clients for all potential blobs
            SetupBlobClient("TestProjectionWithExternalCheckpoint.json");

            // We'll need to setup checkpoint blobs dynamically as fingerprints change
            _containerClient.GetBlobClient(Arg.Is<string>(s => s.StartsWith("checkpoints/")))
                .Returns(callInfo =>
                {
                    var blobName = callInfo.Arg<string>();
                    var blobClient = Substitute.For<BlobClient>();

                    blobClient.ExistsAsync(Arg.Any<CancellationToken>())
                        .Returns(_ => Task.FromResult(Response.FromValue(_blobStorage.ContainsKey(blobName), Substitute.For<Response>())));

                    blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                        .Returns(_ =>
                        {
                            if (_blobStorage.TryGetValue(blobName, out var data))
                            {
                                var content = new BinaryData(data);
                                var downloadResult = BlobsModelFactory.BlobDownloadResult(content);
                                return Task.FromResult(Response.FromValue(downloadResult, Substitute.For<Response>()));
                            }
                            throw new RequestFailedException(404, "Blob not found");
                        });

                    blobClient.UploadAsync(Arg.Any<BinaryData>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                        .Returns(ci =>
                        {
                            var data = ci.Arg<BinaryData>();
                            _blobStorage[blobName] = data.ToArray();
                            return Task.FromResult(Substitute.For<Response<BlobContentInfo>>());
                        });

                    return blobClient;
                });

            // Create test events for different objects
            var event1 = CreateTestEvent("company", "company-1", "CompanyCreated");
            var event2 = CreateTestEvent("company", "company-2", "CompanyCreated");
            var event3 = CreateTestEvent("company", "company-3", "CompanyCreated");

            var versionToken1 = new VersionToken("company", "company-1", "stream", 0);
            var versionToken2 = new VersionToken("company", "company-2", "stream", 0);
            var versionToken3 = new VersionToken("company", "company-3", "stream", 0);

            // Act - Process and save first event
            var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);
            await projection.Fold(event1, versionToken1);
            projection.UpdateCheckpoint(versionToken1);
            await factory.SaveAsync(projection);

            // Verify checkpoint has 1 entry
            Assert.Single(projection.Checkpoint);
            var firstFingerprint = projection.CheckpointFingerprint;
            Assert.NotNull(firstFingerprint);

            // Load projection again
            projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            // Verify checkpoint was loaded with 1 entry
            Assert.Single(projection.Checkpoint);

            // Process and save second event
            await projection.Fold(event2, versionToken2);
            projection.UpdateCheckpoint(versionToken2);
            await factory.SaveAsync(projection);

            // Verify checkpoint now has 2 entries
            Assert.Equal(2, projection.Checkpoint.Count);
            var secondFingerprint = projection.CheckpointFingerprint;
            Assert.NotNull(secondFingerprint);
            Assert.NotEqual(firstFingerprint, secondFingerprint);

            // Load projection again
            projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            // CRITICAL: Verify checkpoint was loaded with ALL 2 entries
            Assert.Equal(2, projection.Checkpoint.Count);
            Assert.Contains(projection.Checkpoint, kv => kv.Key.Value == "company__company-1");
            Assert.Contains(projection.Checkpoint, kv => kv.Key.Value == "company__company-2");

            // Process and save third event
            await projection.Fold(event3, versionToken3);
            projection.UpdateCheckpoint(versionToken3);
            await factory.SaveAsync(projection);

            // Verify checkpoint now has 3 entries
            Assert.Equal(3, projection.Checkpoint.Count);

            // Load projection one final time
            projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            // CRITICAL: Verify checkpoint was loaded with ALL 3 entries
            Assert.Equal(3, projection.Checkpoint.Count);
            Assert.Contains(projection.Checkpoint, kv => kv.Key.Value == "company__company-1");
            Assert.Contains(projection.Checkpoint, kv => kv.Key.Value == "company__company-2");
            Assert.Contains(projection.Checkpoint, kv => kv.Key.Value == "company__company-3");
        }

        [Fact]
        public async Task Should_preserve_checkpoint_when_saving_updates_to_existing_objects()
        {
            // Arrange
            var factory = new TestProjectionFactory(_clientFactory, "test-connection", "test-container");
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            SetupBlobClient("TestProjectionWithExternalCheckpoint.json");

            _containerClient.GetBlobClient(Arg.Is<string>(s => s.StartsWith("checkpoints/")))
                .Returns(callInfo =>
                {
                    var blobName = callInfo.Arg<string>();
                    var blobClient = Substitute.For<BlobClient>();

                    blobClient.ExistsAsync(Arg.Any<CancellationToken>())
                        .Returns(_ => Task.FromResult(Response.FromValue(_blobStorage.ContainsKey(blobName), Substitute.For<Response>())));

                    blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                        .Returns(_ =>
                        {
                            if (_blobStorage.TryGetValue(blobName, out var data))
                            {
                                var content = new BinaryData(data);
                                var downloadResult = BlobsModelFactory.BlobDownloadResult(content);
                                return Task.FromResult(Response.FromValue(downloadResult, Substitute.For<Response>()));
                            }
                            throw new RequestFailedException(404, "Blob not found");
                        });

                    blobClient.UploadAsync(Arg.Any<BinaryData>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                        .Returns(ci =>
                        {
                            var data = ci.Arg<BinaryData>();
                            _blobStorage[blobName] = data.ToArray();
                            return Task.FromResult(Substitute.For<Response<BlobContentInfo>>());
                        });

                    return blobClient;
                });

            // Create events - first batch
            var event1 = CreateTestEvent("company", "company-1", "CompanyCreated");
            var event2 = CreateTestEvent("company", "company-2", "CompanyCreated");
            var versionToken1 = new VersionToken("company", "company-1", "stream", 0);
            var versionToken2 = new VersionToken("company", "company-2", "stream", 0);

            // Act - Process first batch
            var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);
            await projection.Fold(event1, versionToken1);
            projection.UpdateCheckpoint(versionToken1);
            await projection.Fold(event2, versionToken2);
            projection.UpdateCheckpoint(versionToken2);
            await factory.SaveAsync(projection);

            Assert.Equal(2, projection.Checkpoint.Count);

            // Now update company-1 with a new event
            var event1Update = CreateTestEvent("company", "company-1", "CompanyUpdated");
            var versionToken1Update = new VersionToken("company", "company-1", "stream", 1);

            projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            // CRITICAL: Checkpoint should have 2 entries after load
            Assert.Equal(2, projection.Checkpoint.Count);

            await projection.Fold(event1Update, versionToken1Update);
            projection.UpdateCheckpoint(versionToken1Update);
            await factory.SaveAsync(projection);

            // Checkpoint should still have 2 entries (company-1 updated, company-2 unchanged)
            Assert.Equal(2, projection.Checkpoint.Count);

            // Reload and verify
            projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);
            Assert.Equal(2, projection.Checkpoint.Count);
            Assert.Contains(projection.Checkpoint, kv => kv.Key.Value == "company__company-1");
            Assert.Contains(projection.Checkpoint, kv => kv.Key.Value == "company__company-2");

            // Verify company-1 has updated version
            var company1Entry = projection.Checkpoint.First(kv => kv.Key.Value == "company__company-1");
            Assert.Contains("00000000000000000001", company1Entry.Value.Value); // Version 1
        }

        [Fact]
        public async Task External_checkpoint_file_should_contain_all_entries()
        {
            // Arrange
            var factory = new TestProjectionFactory(_clientFactory, "test-connection", "test-container");
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            SetupBlobClient("TestProjectionWithExternalCheckpoint.json");

            _containerClient.GetBlobClient(Arg.Is<string>(s => s.StartsWith("checkpoints/")))
                .Returns(callInfo =>
                {
                    var blobName = callInfo.Arg<string>();
                    var blobClient = Substitute.For<BlobClient>();

                    blobClient.ExistsAsync(Arg.Any<CancellationToken>())
                        .Returns(_ => Task.FromResult(Response.FromValue(_blobStorage.ContainsKey(blobName), Substitute.For<Response>())));

                    blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                        .Returns(_ =>
                        {
                            if (_blobStorage.TryGetValue(blobName, out var data))
                            {
                                var content = new BinaryData(data);
                                var downloadResult = BlobsModelFactory.BlobDownloadResult(content);
                                return Task.FromResult(Response.FromValue(downloadResult, Substitute.For<Response>()));
                            }
                            throw new RequestFailedException(404, "Blob not found");
                        });

                    blobClient.UploadAsync(Arg.Any<BinaryData>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                        .Returns(ci =>
                        {
                            var data = ci.Arg<BinaryData>();
                            _blobStorage[blobName] = data.ToArray();
                            return Task.FromResult(Substitute.For<Response<BlobContentInfo>>());
                        });

                    return blobClient;
                });

            // Create events
            var event1 = CreateTestEvent("company", "company-1", "CompanyCreated");
            var event2 = CreateTestEvent("company", "company-2", "CompanyCreated");
            var event3 = CreateTestEvent("company", "company-3", "CompanyCreated");
            var versionToken1 = new VersionToken("company", "company-1", "stream", 0);
            var versionToken2 = new VersionToken("company", "company-2", "stream", 0);
            var versionToken3 = new VersionToken("company", "company-3", "stream", 0);

            // Act - Process all events and save
            var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);
            await projection.Fold(event1, versionToken1);
            projection.UpdateCheckpoint(versionToken1);
            await projection.Fold(event2, versionToken2);
            projection.UpdateCheckpoint(versionToken2);
            await projection.Fold(event3, versionToken3);
            projection.UpdateCheckpoint(versionToken3);
            await factory.SaveAsync(projection);

            // Assert - Check the checkpoint file content directly
            var checkpointBlobName = $"checkpoints/TestProjectionWithExternalCheckpoint/{projection.CheckpointFingerprint}.json";
            Assert.True(_blobStorage.ContainsKey(checkpointBlobName), "Checkpoint file should exist");

            var checkpointJson = Encoding.UTF8.GetString(_blobStorage[checkpointBlobName]);
            var checkpoint = JsonSerializer.Deserialize(checkpointJson, TestProjectionJsonContext.Default.Checkpoint);

            Assert.NotNull(checkpoint);
            Assert.Equal(3, checkpoint.Count);
            Assert.Contains(checkpoint, kv => kv.Key.Value == "company__company-1");
            Assert.Contains(checkpoint, kv => kv.Key.Value == "company__company-2");
            Assert.Contains(checkpoint, kv => kv.Key.Value == "company__company-3");
        }

        private static IEvent CreateTestEvent(string objectName, string objectId, string eventType)
        {
            var @event = Substitute.For<IEvent>();
            @event.EventType.Returns(eventType);
            return @event;
        }

        [Fact]
        public async Task Bug_repro_checkpoint_resets_when_projection_created_fresh_each_time()
        {
            // This test reproduces the bug where a NEW projection instance is created
            // instead of loading the existing one, causing checkpoint data loss.

            var factory = new TestProjectionFactory(_clientFactory, "test-connection", "test-container");
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            SetupBlobClient("TestProjectionWithExternalCheckpoint.json");

            _containerClient.GetBlobClient(Arg.Is<string>(s => s.StartsWith("checkpoints/")))
                .Returns(callInfo =>
                {
                    var blobName = callInfo.Arg<string>();
                    var blobClient = Substitute.For<BlobClient>();

                    blobClient.ExistsAsync(Arg.Any<CancellationToken>())
                        .Returns(_ => Task.FromResult(Response.FromValue(_blobStorage.ContainsKey(blobName), Substitute.For<Response>())));

                    blobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                        .Returns(_ =>
                        {
                            if (_blobStorage.TryGetValue(blobName, out var data))
                            {
                                var content = new BinaryData(data);
                                var downloadResult = BlobsModelFactory.BlobDownloadResult(content);
                                return Task.FromResult(Response.FromValue(downloadResult, Substitute.For<Response>()));
                            }
                            throw new RequestFailedException(404, "Blob not found");
                        });

                    blobClient.UploadAsync(Arg.Any<BinaryData>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                        .Returns(ci =>
                        {
                            var data = ci.Arg<BinaryData>();
                            _blobStorage[blobName] = data.ToArray();
                            return Task.FromResult(Substitute.For<Response<BlobContentInfo>>());
                        });

                    return blobClient;
                });

            var event1 = CreateTestEvent("company", "company-1", "CompanyCreated");
            var event2 = CreateTestEvent("company", "company-2", "CompanyCreated");
            var versionToken1 = new VersionToken("company", "company-1", "stream", 0);
            var versionToken2 = new VersionToken("company", "company-2", "stream", 0);

            // BUGGY PATTERN: Create fresh projection, process, save
            // This simulates what might happen if code creates New() instead of GetOrCreateAsync()
            var projection1 = new TestProjectionWithExternalCheckpoint();
            await projection1.Fold(event1, versionToken1);
            projection1.UpdateCheckpoint(versionToken1);

            // At this point checkpoint has 1 entry
            Assert.Single(projection1.Checkpoint);
            var fingerprint1 = projection1.CheckpointFingerprint;

            // Simulate saving (manually, as we're testing the bug pattern)
            _blobStorage["TestProjectionWithExternalCheckpoint.json"] = Encoding.UTF8.GetBytes(projection1.ToJson());
            _blobStorage[$"checkpoints/TestProjectionWithExternalCheckpoint/{fingerprint1}.json"] =
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(projection1.Checkpoint, TestProjectionJsonContext.Default.Checkpoint));

            // BUGGY: Create another FRESH projection instead of loading
            var projection2 = new TestProjectionWithExternalCheckpoint();
            await projection2.Fold(event2, versionToken2);
            projection2.UpdateCheckpoint(versionToken2);

            // BUG MANIFEST: projection2 only has 1 entry (company-2), not 2 entries
            Assert.Single(projection2.Checkpoint); // This is the bug - should be 2 if loaded properly

            // The fingerprint is calculated from just 1 entry
            var fingerprint2 = projection2.CheckpointFingerprint;
            Assert.NotEqual(fingerprint1, fingerprint2); // Different fingerprints

            // Save creates a NEW checkpoint file with just 1 entry
            _blobStorage[$"checkpoints/TestProjectionWithExternalCheckpoint/{fingerprint2}.json"] =
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(projection2.Checkpoint, TestProjectionJsonContext.Default.Checkpoint));

            // Verify both checkpoint files have only 1 entry each
            var checkpoint1Json = Encoding.UTF8.GetString(_blobStorage[$"checkpoints/TestProjectionWithExternalCheckpoint/{fingerprint1}.json"]);
            var checkpoint2Json = Encoding.UTF8.GetString(_blobStorage[$"checkpoints/TestProjectionWithExternalCheckpoint/{fingerprint2}.json"]);

            var checkpoint1 = JsonSerializer.Deserialize(checkpoint1Json, TestProjectionJsonContext.Default.Checkpoint);
            var checkpoint2 = JsonSerializer.Deserialize(checkpoint2Json, TestProjectionJsonContext.Default.Checkpoint);

            // Both checkpoints have only 1 entry - this demonstrates the bug
            Assert.Single(checkpoint1!);
            Assert.Single(checkpoint2!);

            // This is the CORRECT pattern - use GetOrCreateAsync
            var projection3 = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

            // After loading with GetOrCreateAsync, checkpoint should have the entry from the last save
            // (which was fingerprint2 with company-2)
            Assert.Single(projection3.Checkpoint); // Still only 1 because we're loading the buggy state
        }
    }
}
