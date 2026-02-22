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
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

/// <summary>
/// Tests for verifying projection status management in BlobProjectionFactory.
/// </summary>
public partial class BlobProjectionFactoryStatusTests
{
    /// <summary>
    /// Test projection with external checkpoint enabled.
    /// </summary>
    [JsonSerializable(typeof(TestProjectionForStatus))]
    [JsonSerializable(typeof(Checkpoint))]
    [JsonSerializable(typeof(ProjectionStatus))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal partial class TestProjectionStatusJsonContext : JsonSerializerContext
    {
    }

    public class TestProjectionForStatus : Projection
    {
        private Checkpoint _checkpoint = [];

        [JsonPropertyName("items")]
        public Dictionary<string, string> Items { get; set; } = [];

        [JsonIgnore]
        public override Checkpoint Checkpoint
        {
            get => _checkpoint;
            set => _checkpoint = value;
        }

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? context = null) where T : class
        {
            Items[versionToken.ObjectId] = @event.EventType;
            return Task.CompletedTask;
        }

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } = [];

        public override string ToJson()
        {
            return JsonSerializer.Serialize(this, TestProjectionStatusJsonContext.Default.TestProjectionForStatus);
        }

        public static TestProjectionForStatus? LoadFromJson(string json)
        {
            return JsonSerializer.Deserialize(json, TestProjectionStatusJsonContext.Default.TestProjectionForStatus);
        }
    }

    public class TestProjectionStatusFactory : BlobProjectionFactory<TestProjectionForStatus>
    {
        public TestProjectionStatusFactory(
            IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
            string connectionName,
            string containerOrPath)
            : base(blobServiceClientFactory, connectionName, containerOrPath)
        {
        }

        protected override bool HasExternalCheckpoint => true;

        protected override TestProjectionForStatus New() => new();

        protected override TestProjectionForStatus? LoadFromJson(
            string json,
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory)
        {
            return TestProjectionForStatus.LoadFromJson(json);
        }
    }

    private readonly IAzureClientFactory<BlobServiceClient> _clientFactory;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly Dictionary<string, byte[]> _blobStorage;

    public BlobProjectionFactoryStatusTests()
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

    private void SetupAllBlobClients()
    {
        SetupBlobClient("TestProjectionForStatus.json");

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
    }

    [Fact]
    public async Task GetStatusAsync_returns_Active_for_new_projection()
    {
        // Arrange
        SetupAllBlobClients();
        var factory = new TestProjectionStatusFactory(_clientFactory, "test-connection", "test-container");

        // Act
        var status = await factory.GetStatusAsync();

        // Assert
        Assert.Equal(ProjectionStatus.Active, status);
    }

    [Fact]
    public async Task SetStatusAsync_persists_Rebuilding_status()
    {
        // Arrange
        SetupAllBlobClients();
        var factory = new TestProjectionStatusFactory(_clientFactory, "test-connection", "test-container");
        var documentFactory = Substitute.For<IObjectDocumentFactory>();
        var eventStreamFactory = Substitute.For<IEventStreamFactory>();

        // Create and save initial projection
        var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);
        await factory.SaveAsync(projection);

        // Act
        await factory.SetStatusAsync(ProjectionStatus.Rebuilding);

        // Assert
        var status = await factory.GetStatusAsync();
        Assert.Equal(ProjectionStatus.Rebuilding, status);
    }

    [Fact]
    public async Task SetStatusAsync_persists_Disabled_status()
    {
        // Arrange
        SetupAllBlobClients();
        var factory = new TestProjectionStatusFactory(_clientFactory, "test-connection", "test-container");
        var documentFactory = Substitute.For<IObjectDocumentFactory>();
        var eventStreamFactory = Substitute.For<IEventStreamFactory>();

        // Create and save initial projection
        var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);
        await factory.SaveAsync(projection);

        // Act
        await factory.SetStatusAsync(ProjectionStatus.Disabled);

        // Assert
        var status = await factory.GetStatusAsync();
        Assert.Equal(ProjectionStatus.Disabled, status);
    }

    [Fact]
    public async Task Status_survives_save_load_cycle()
    {
        // Arrange
        SetupAllBlobClients();
        var factory = new TestProjectionStatusFactory(_clientFactory, "test-connection", "test-container");
        var documentFactory = Substitute.For<IObjectDocumentFactory>();
        var eventStreamFactory = Substitute.For<IEventStreamFactory>();

        // Create projection and set status
        var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);
        projection.Status = ProjectionStatus.Rebuilding;
        await factory.SaveAsync(projection);

        // Act - Load projection again
        var loadedProjection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

        // Assert
        Assert.Equal(ProjectionStatus.Rebuilding, loadedProjection.Status);
    }

    [Fact]
    public async Task SetStatusAsync_then_SetStatusAsync_Active_restores_normal_operation()
    {
        // Arrange
        SetupAllBlobClients();
        var factory = new TestProjectionStatusFactory(_clientFactory, "test-connection", "test-container");
        var documentFactory = Substitute.For<IObjectDocumentFactory>();
        var eventStreamFactory = Substitute.For<IEventStreamFactory>();

        var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);
        await factory.SaveAsync(projection);

        // Set to Rebuilding
        await factory.SetStatusAsync(ProjectionStatus.Rebuilding);
        Assert.Equal(ProjectionStatus.Rebuilding, await factory.GetStatusAsync());

        // Act - Set back to Active
        await factory.SetStatusAsync(ProjectionStatus.Active);

        // Assert
        Assert.Equal(ProjectionStatus.Active, await factory.GetStatusAsync());
    }

    [Fact]
    public async Task New_projection_has_Active_status_by_default()
    {
        // Arrange
        SetupAllBlobClients();
        var factory = new TestProjectionStatusFactory(_clientFactory, "test-connection", "test-container");
        var documentFactory = Substitute.For<IObjectDocumentFactory>();
        var eventStreamFactory = Substitute.For<IEventStreamFactory>();

        // Act
        var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

        // Assert
        Assert.Equal(ProjectionStatus.Active, projection.Status);
    }

    [Fact]
    public async Task SchemaVersion_is_set_on_new_projection()
    {
        // Arrange
        SetupAllBlobClients();
        var factory = new TestProjectionStatusFactory(_clientFactory, "test-connection", "test-container");
        var documentFactory = Substitute.For<IObjectDocumentFactory>();
        var eventStreamFactory = Substitute.For<IEventStreamFactory>();

        // Act
        var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory);

        // Assert - New projection should have SchemaVersion = CodeSchemaVersion
        Assert.Equal(projection.CodeSchemaVersion, projection.SchemaVersion);
    }
}
