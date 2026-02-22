#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using System.Threading.Tasks;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table;
using Microsoft.Extensions.Azure;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableObjectIdProviderTests
{
    private readonly IAzureClientFactory<TableServiceClient> clientFactory;
    private readonly TableServiceClient tableServiceClient;
    private readonly TableClient tableClient;
    private readonly EventStreamTableSettings settings;

    public TableObjectIdProviderTests()
    {
        clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
        tableServiceClient = Substitute.For<TableServiceClient>();
        tableClient = Substitute.For<TableClient>();
        settings = new EventStreamTableSettings("test-connection");

        // Setup table client chain
        clientFactory.CreateClient(Arg.Any<string>()).Returns(tableServiceClient);
        tableServiceClient.GetTableClient(Arg.Any<string>()).Returns(tableClient);
    }

    public class Constructor : TableObjectIdProviderTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_client_factory_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TableObjectIdProvider(null!, settings));
        }

        [Fact]
        public void Should_throw_argument_null_exception_when_settings_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TableObjectIdProvider(clientFactory, null!));
        }

        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            // Act
            var sut = new TableObjectIdProvider(clientFactory, settings);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class GetObjectIdsAsync : TableObjectIdProviderTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            // Arrange
            var sut = new TableObjectIdProvider(clientFactory, settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetObjectIdsAsync(null!, null, 10));
        }

        [Fact]
        public async Task Should_throw_argument_out_of_range_exception_when_page_size_is_less_than_one()
        {
            // Arrange
            var sut = new TableObjectIdProvider(clientFactory, settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                sut.GetObjectIdsAsync("TestObject", null, 0));
        }
    }

    public class ExistsAsync : TableObjectIdProviderTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            // Arrange
            var sut = new TableObjectIdProvider(clientFactory, settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ExistsAsync(null!, "test-id"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            // Arrange
            var sut = new TableObjectIdProvider(clientFactory, settings);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ExistsAsync("TestObject", null!));
        }
    }
}
