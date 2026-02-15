#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Projections;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableProjectionStatusCoordinatorTests
{
    protected readonly TableServiceClient TableServiceClient;
    protected readonly TableClient TableClient;

    public TableProjectionStatusCoordinatorTests()
    {
        TableServiceClient = Substitute.For<TableServiceClient>();
        TableClient = Substitute.For<TableClient>();
        TableServiceClient.GetTableClient(Arg.Any<string>()).Returns(TableClient);
        TableClient.CreateIfNotExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response<Azure.Data.Tables.Models.TableItem>>());
    }

    protected TableProjectionStatusCoordinator CreateSut(string tableName = "ProjectionStatus") =>
        new(TableServiceClient, tableName);

    public class Constructor : TableProjectionStatusCoordinatorTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_tableServiceClient_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TableProjectionStatusCoordinator(null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = CreateSut();
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_use_default_table_name()
        {
            var sut = CreateSut();
            Assert.NotNull(sut);
            Assert.IsType<TableProjectionStatusCoordinator>(sut);
        }
    }

    public class StartRebuildAsyncMethod : TableProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_projectionName_is_null()
        {
            var sut = CreateSut();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync(null!, "object-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30)));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_objectId_is_null()
        {
            var sut = CreateSut();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.StartRebuildAsync("TestProjection", null!, RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(30)));
        }
    }

    public class GetStatusAsyncMethod : TableProjectionStatusCoordinatorTests
    {
        [Fact]
        public async Task Should_return_null_when_entity_not_found()
        {
            // Arrange
            var sut = CreateSut();
            var response = Substitute.For<NullableResponse<ProjectionStatusEntity>>();
            response.HasValue.Returns(false);
            TableClient.GetEntityIfExistsAsync<ProjectionStatusEntity>(
                Arg.Any<string>(), Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()).Returns(response);

            // Act
            var result = await sut.GetStatusAsync("TestProjection", "object-1");

            // Assert
            Assert.Null(result);
        }
    }
}
