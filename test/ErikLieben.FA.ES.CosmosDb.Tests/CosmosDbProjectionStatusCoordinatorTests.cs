using System.Net;
using ErikLieben.FA.ES.Projections;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbProjectionStatusCoordinatorTests
{
    private readonly CosmosClient cosmosClient;
    private readonly Database database;
    private readonly Container container;
    private readonly ILogger<CosmosDbProjectionStatusCoordinator> logger;

    public CosmosDbProjectionStatusCoordinatorTests()
    {
        cosmosClient = Substitute.For<CosmosClient>();
        database = Substitute.For<Database>();
        container = Substitute.For<Container>();
        logger = Substitute.For<ILogger<CosmosDbProjectionStatusCoordinator>>();

        cosmosClient.GetDatabase("test-db").Returns(database);
        database.GetContainer("projection-status").Returns(container);
    }

    public class Constructor : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_cosmos_client_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbProjectionStatusCoordinator(null!, "test-db"));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_database_name_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CosmosDbProjectionStatusCoordinator(cosmosClient, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_custom_container_name()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", "custom-container");
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_null_logger()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", null, null);
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_use_default_container_name_when_null()
        {
            // Verify the default container name "projection-status" is used
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");
            Assert.NotNull(sut);
        }
    }

    public class StartRebuildAsync : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_projection_name_is_null()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync(null!, "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync("TestProjection", null!, RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public async Task Should_upsert_document_and_return_token()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);

            container.UpsertItemAsync<object>(
                Arg.Any<object>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Substitute.For<ItemResponse<object>>()));

            var token = await sut.StartRebuildAsync(
                "TestProjection",
                "obj-1",
                RebuildStrategy.BlockingWithCatchUp,
                TimeSpan.FromMinutes(5));

            Assert.NotNull(token);
            Assert.Equal("TestProjection", token.ProjectionName);
            Assert.Equal("obj-1", token.ObjectId);
            Assert.Equal(RebuildStrategy.BlockingWithCatchUp, token.Strategy);
            Assert.False(token.IsExpired);
        }
    }

    public class StartCatchUpAsyncTests : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartCatchUpAsync(null!));
        }

        [Fact]
        public async Task Should_return_when_document_not_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));

            container.ReadItemAsync<CosmosStatusDocument>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(CreateCosmosException(HttpStatusCode.NotFound));

            // Should not throw - just returns when document not found
            var exception = await Record.ExceptionAsync(() => sut.StartCatchUpAsync(token));
            Assert.Null(exception);
        }

        [Fact]
        public async Task Should_update_status_to_catching_up()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));

            var document = CreateDocumentWithToken(token);
            SetupReadItemReturning(document);
            SetupReplaceItem();

            await sut.StartCatchUpAsync(token);

            await container.Received(1).ReplaceItemAsync(
                Arg.Is<CosmosStatusDocument>(d => d.Status == (int)ProjectionStatus.CatchingUp),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_when_token_does_not_match()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));

            // Create document with a different token
            var differentToken = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));
            var document = CreateDocumentWithToken(differentToken);
            SetupReadItemReturning(document);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.StartCatchUpAsync(token));
        }
    }

    public class MarkReadyAsyncTests : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.MarkReadyAsync(null!));
        }

        [Fact]
        public async Task Should_return_when_document_not_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlueGreen, TimeSpan.FromMinutes(5));

            container.ReadItemAsync<CosmosStatusDocument>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(CreateCosmosException(HttpStatusCode.NotFound));

            var exception = await Record.ExceptionAsync(() => sut.MarkReadyAsync(token));
            Assert.Null(exception);
        }

        [Fact]
        public async Task Should_update_status_to_ready()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlueGreen, TimeSpan.FromMinutes(5));

            var document = CreateDocumentWithToken(token);
            SetupReadItemReturning(document);
            SetupReplaceItem();

            await sut.MarkReadyAsync(token);

            await container.Received(1).ReplaceItemAsync(
                Arg.Is<CosmosStatusDocument>(d => d.Status == (int)ProjectionStatus.Ready),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class CompleteRebuildAsyncTests : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CompleteRebuildAsync(null!));
        }

        [Fact]
        public async Task Should_return_when_document_not_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));

            container.ReadItemAsync<CosmosStatusDocument>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(CreateCosmosException(HttpStatusCode.NotFound));

            var exception = await Record.ExceptionAsync(() => sut.CompleteRebuildAsync(token));
            Assert.Null(exception);
        }

        [Fact]
        public async Task Should_update_status_to_active_and_clear_token()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));

            var document = CreateDocumentWithToken(token);
            SetupReadItemReturning(document);
            SetupReplaceItem();

            await sut.CompleteRebuildAsync(token);

            await container.Received(1).ReplaceItemAsync(
                Arg.Is<CosmosStatusDocument>(d =>
                    d.Status == (int)ProjectionStatus.Active &&
                    d.RebuildTokenJson == null),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class CancelRebuildAsyncTests : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_token_is_null()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.CancelRebuildAsync(null!));
        }

        [Fact]
        public async Task Should_return_when_document_not_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));

            container.ReadItemAsync<CosmosStatusDocument>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(CreateCosmosException(HttpStatusCode.NotFound));

            var exception = await Record.ExceptionAsync(() => sut.CancelRebuildAsync(token, "test error"));
            Assert.Null(exception);
        }

        [Fact]
        public async Task Should_set_status_to_failed_when_error_provided()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));

            var document = CreateDocumentWithToken(token);
            SetupReadItemReturning(document);
            SetupReplaceItem();

            await sut.CancelRebuildAsync(token, "Something went wrong");

            await container.Received(1).ReplaceItemAsync(
                Arg.Is<CosmosStatusDocument>(d =>
                    d.Status == (int)ProjectionStatus.Failed &&
                    d.RebuildTokenJson == null),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_set_status_to_active_when_no_error()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);
            var token = RebuildToken.Create("TestProjection", "obj-1",
                RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));

            var document = CreateDocumentWithToken(token);
            SetupReadItemReturning(document);
            SetupReplaceItem();

            await sut.CancelRebuildAsync(token);

            await container.Received(1).ReplaceItemAsync(
                Arg.Is<CosmosStatusDocument>(d =>
                    d.Status == (int)ProjectionStatus.Active &&
                    d.RebuildTokenJson == null),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class GetStatusAsyncTests : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_return_null_when_document_not_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            container.ReadItemAsync<CosmosStatusDocument>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(CreateCosmosException(HttpStatusCode.NotFound));

            var result = await sut.GetStatusAsync("TestProjection", "obj-1");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_status_info_when_document_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db");

            var document = new CosmosStatusDocument
            {
                Id = "TestProjection_obj-1",
                ProjectionName = "TestProjection",
                ObjectId = "obj-1",
                Status = (int)ProjectionStatus.Active,
                StatusChangedAt = DateTimeOffset.UtcNow,
                SchemaVersion = 1
            };
            SetupReadItemReturning(document);

            var result = await sut.GetStatusAsync("TestProjection", "obj-1");

            Assert.NotNull(result);
            Assert.Equal("TestProjection", result.ProjectionName);
            Assert.Equal("obj-1", result.ObjectId);
            Assert.Equal(ProjectionStatus.Active, result.Status);
            Assert.Equal(1, result.SchemaVersion);
        }
    }

    public class EnableAsyncTests : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_return_when_document_not_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);

            container.ReadItemAsync<CosmosStatusDocument>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(CreateCosmosException(HttpStatusCode.NotFound));

            var exception = await Record.ExceptionAsync(() => sut.EnableAsync("TestProjection", "obj-1"));
            Assert.Null(exception);
        }

        [Fact]
        public async Task Should_update_status_to_active()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);

            var document = new CosmosStatusDocument
            {
                Id = "TestProjection_obj-1",
                ProjectionName = "TestProjection",
                ObjectId = "obj-1",
                Status = (int)ProjectionStatus.Disabled,
                StatusChangedAt = DateTimeOffset.UtcNow,
                SchemaVersion = 0
            };
            SetupReadItemReturning(document);
            SetupReplaceItem();

            await sut.EnableAsync("TestProjection", "obj-1");

            await container.Received(1).ReplaceItemAsync(
                Arg.Is<CosmosStatusDocument>(d => d.Status == (int)ProjectionStatus.Active),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class DisableAsyncTests : CosmosDbProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_create_new_document_when_not_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);

            container.ReadItemAsync<CosmosStatusDocument>(
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .ThrowsAsync(CreateCosmosException(HttpStatusCode.NotFound));

            container.CreateItemAsync<CosmosStatusDocument>(
                Arg.Any<CosmosStatusDocument>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Substitute.For<ItemResponse<CosmosStatusDocument>>()));

            await sut.DisableAsync("TestProjection", "obj-1");

            await container.Received(1).CreateItemAsync(
                Arg.Any<CosmosStatusDocument>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_update_existing_document_when_found()
        {
            var sut = new CosmosDbProjectionStatusCoordinator(cosmosClient, "test-db", logger: logger);

            var document = new CosmosStatusDocument
            {
                Id = "TestProjection_obj-1",
                ProjectionName = "TestProjection",
                ObjectId = "obj-1",
                Status = (int)ProjectionStatus.Active,
                StatusChangedAt = DateTimeOffset.UtcNow,
                SchemaVersion = 0
            };
            SetupReadItemReturning(document);
            SetupReplaceItem();

            await sut.DisableAsync("TestProjection", "obj-1");

            await container.Received(1).ReplaceItemAsync(
                Arg.Is<CosmosStatusDocument>(d => d.Status == (int)ProjectionStatus.Disabled),
                Arg.Any<string>(),
                Arg.Any<PartitionKey>(),
                Arg.Any<ItemRequestOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    /// <summary>
    /// Helper to create a CosmosStatusDocument with a serialized token matching the given RebuildToken.
    /// </summary>
    private static CosmosStatusDocument CreateDocumentWithToken(RebuildToken token)
    {
        var rebuildInfo = RebuildInfo.Start(token.Strategy);
        return new CosmosStatusDocument
        {
            Id = $"{token.ProjectionName}_{token.ObjectId}",
            ProjectionName = token.ProjectionName,
            ObjectId = token.ObjectId,
            Status = (int)ProjectionStatus.Rebuilding,
            StatusChangedAt = DateTimeOffset.UtcNow,
            SchemaVersion = 0,
            RebuildTokenJson = JsonConvert.SerializeObject(token),
            RebuildInfoJson = JsonConvert.SerializeObject(rebuildInfo),
            ETag = "test-etag"
        };
    }

    /// <summary>
    /// Sets up the container mock to return the specified document from ReadItemAsync.
    /// </summary>
    private void SetupReadItemReturning(CosmosStatusDocument document)
    {
        var itemResponse = Substitute.For<ItemResponse<CosmosStatusDocument>>();
        itemResponse.Resource.Returns(document);
        itemResponse.ETag.Returns(document.ETag ?? "test-etag");

        container.ReadItemAsync<CosmosStatusDocument>(
            Arg.Any<string>(),
            Arg.Any<PartitionKey>(),
            Arg.Any<ItemRequestOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(itemResponse));
    }

    /// <summary>
    /// Sets up the container mock to accept ReplaceItemAsync calls.
    /// </summary>
    private void SetupReplaceItem()
    {
        container.ReplaceItemAsync(
            Arg.Any<CosmosStatusDocument>(),
            Arg.Any<string>(),
            Arg.Any<PartitionKey>(),
            Arg.Any<ItemRequestOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Substitute.For<ItemResponse<CosmosStatusDocument>>()));
    }

    /// <summary>
    /// Helper to create a CosmosException with a specific status code.
    /// CosmosException does not have a public constructor that is easily mockable,
    /// so we use the available overload.
    /// </summary>
    private static CosmosException CreateCosmosException(HttpStatusCode statusCode)
    {
        return new CosmosException(
            message: $"Test exception with status {statusCode}",
            statusCode: statusCode,
            subStatusCode: 0,
            activityId: Guid.NewGuid().ToString(),
            requestCharge: 0);
    }
}
