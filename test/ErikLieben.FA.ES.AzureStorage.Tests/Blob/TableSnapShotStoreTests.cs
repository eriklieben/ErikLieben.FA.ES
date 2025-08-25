// using System.Text.Json;
// using System.Text.Json.Serialization.Metadata;
// using Azure;
// using Azure.Data.Tables;
// using ErikLieben.FA.ES.AzureStorage.Configuration;
// using ErikLieben.FA.ES.AzureStorage.Table;
// using ErikLieben.FA.ES.Documents;
// using ErikLieben.FA.ES.Processors;
// using Microsoft.Extensions.Azure;
// using NSubstitute;
//
// namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;
//
// public class TableSnapShotStoreTests
// {
//     public class SetAsync
//     {
//         [Fact]
//         public async Task Should_save_snapshot_with_expected_keys_and_table_name()
//         {
//             // Arrange
//             var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
//             var settings = new EventStreamTableSettings("defaultStore") { AutoCreateTable = true };
//             var sut = new TableSnapShotStore(clientFactory, settings);
//
//             var obj = Substitute.For<IBase>();
//             var document = Substitute.For<IObjectDocument>();
//             var active = Substitute.For<StreamInformation>();
//             active.StreamIdentifier = "stream-ABC";
//             active.SnapShotConnectionName = "snapshot-conn";
//             document.Active.Returns(active);
//             document.ObjectName.Returns("My Obj!");
//             document.ObjectId.Returns("id-1");
//
//             var tsc = Substitute.For<TableServiceClient>();
//             var tableClient = Substitute.For<TableClient>();
//
//             clientFactory.CreateClient("snapshot-conn").Returns(tsc);
//             tsc.GetTableClient("snapshotMyObj").Returns(tableClient);
//
//             // Act
//             await sut.SetAsync(obj, TestJsonContext.Default.TestEntity, document, 7);
//
//             // Assert
//             await tableClient.Received(1).CreateIfNotExistsAsync();
//             await tableClient.Received(1).UpsertEntityAsync(Arg.Is<TableEntity>(e =>
//                 e.PartitionKey == "stream-ABC" &&
//                 e.RowKey == 7.ToString("d20") &&
//                 e.GetString("objectId") == "id-1" &&
//                 e.GetString("objectName") == "My Obj!" &&
//                 !string.IsNullOrEmpty(e.GetString("data"))
//             ), TableUpdateMode.Replace);
//             tsc.Received(1).GetTableClient("snapshotMyObj");
//         }
//
//         [Fact]
//         public async Task Should_include_name_suffix_in_row_key_when_provided()
//         {
//             // Arrange
//             var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
//             var settings = new EventStreamTableSettings("defaultStore");
//             var sut = new TableSnapShotStore(clientFactory, settings);
//
//             var obj = new TestEntity { Name = "x" };
//             var document = CreateDoc();
//
//             var tsc = Substitute.For<TableServiceClient>();
//             var tableClient = Substitute.For<TableClient>();
//
//             clientFactory.CreateClient(document.Active.SnapShotConnectionName).Returns(tsc);
//             tsc.GetTableClient(Arg.Any<string>()).Returns(tableClient);
//
//             // Act
//             await sut.SetAsync(obj, TestJsonContext.Default.TestEntity, document, 1, "foo");
//
//             // Assert
//             await tableClient.Received(1).UpsertEntityAsync(Arg.Is<TableEntity>(e => e.RowKey.EndsWith("_foo")), TableUpdateMode.Replace);
//         }
//     }
//
//     public class GetAsyncGeneric
//     {
//         [Fact]
//         public async Task Should_return_null_when_entity_missing_or_data_empty()
//         {
//             // Arrange
//             var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
//             var settings = new EventStreamTableSettings("s");
//             var sut = new TableSnapShotStore(clientFactory, settings);
//             var document = CreateDoc();
//             var tsc = Substitute.For<TableServiceClient>();
//             var tableClient = Substitute.For<TableClient>();
//             clientFactory.CreateClient(document.Active.SnapShotConnectionName).Returns(tsc);
//             tsc.GetTableClient(Arg.Any<string>()).Returns(tableClient);
//
//             // No value
//             Response<TableEntity> noValue = default!;
//             tableClient.GetEntityIfExistsAsync<TableEntity>(Arg.Any<string>(), Arg.Any<string>())
//                 .Returns(_ => noValue);
//             var result1 = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 1);
//             Assert.Null(result1);
//
//             // Empty data
//             var entity = new TableEntity(document.Active.StreamIdentifier, 1.ToString("d20"))
//             {
//                 {"data", ""}
//             };
//             var inner = Substitute.For<Response>();
//             var resp = Response.FromValue(entity, inner);
//             tableClient.GetEntityIfExistsAsync<TableEntity>(Arg.Any<string>(), Arg.Any<string>())
//                 .Returns(Task.FromResult((NullableResponse<TableEntity>)resp));
//             var result2 = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 1);
//             Assert.Null(result2);
//         }
//
//         [Fact]
//         public async Task Should_deserialize_when_entity_present()
//         {
//             // Arrange
//             var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
//             var settings = new EventStreamTableSettings("s");
//             var sut = new TableSnapShotStore(clientFactory, settings);
//             var document = CreateDoc();
//             var tsc = Substitute.For<TableServiceClient>();
//             var tableClient = Substitute.For<TableClient>();
//             clientFactory.CreateClient(document.Active.SnapShotConnectionName).Returns(tsc);
//             tsc.GetTableClient(Arg.Any<string>()).Returns(tableClient);
//
//             var json = JsonSerializer.Serialize(new TestEntity());
//             var entity = new TableEntity(document.Active.StreamIdentifier, 1.ToString("d20"))
//             {
//                 {"data", json}
//             };
//             var inner2 = Substitute.For<Response>();
//             var resp2 = Response.FromValue(entity, inner2);
//             tableClient.GetEntityIfExistsAsync<TableEntity>(Arg.Any<string>(), Arg.Any<string>())
//                 .Returns(_ => (NullableResponse<TableEntity>)resp2);
//
//             // Act
//             var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 1);
//
//             // Assert
//             Assert.NotNull(result);
//         }
//     }
//
//     public class GetAsyncNonGeneric
//     {
//         [Fact]
//         public async Task Should_deserialize_object_when_entity_present()
//         {
//             // Arrange
//             var clientFactory = Substitute.For<IAzureClientFactory<TableServiceClient>>();
//             var settings = new EventStreamTableSettings("s");
//             var sut = new TableSnapShotStore(clientFactory, settings);
//             var document = CreateDoc();
//             var tsc = Substitute.For<TableServiceClient>();
//             var tableClient = Substitute.For<TableClient>();
//             clientFactory.CreateClient(document.Active.SnapShotConnectionName).Returns(tsc);
//             tsc.GetTableClient(Arg.Any<string>()).Returns(tableClient);
//
//             var json = JsonSerializer.Serialize(new TestEntity());
//             var entity = new TableEntity(document.Active.StreamIdentifier, 1.ToString("d20"))
//             {
//                 {"data", json}
//             };
//             var inner3 = Substitute.For<Response>();
//             var resp3 = Response.FromValue(entity, inner3);
//             tableClient.GetEntityIfExistsAsync<TableEntity>(Arg.Any<string>(), Arg.Any<string>())
//                 .Returns(_ => (NullableResponse<TableEntity>)resp3);
//
//             // Act
//             var result = await sut.GetAsync(TestJsonContext.Default.TestEntity, document, 1);
//
//             // Assert
//             Assert.NotNull(result);
//         }
//     }
//
//     private static IObjectDocument CreateDoc()
//     {
//         var doc = Substitute.For<IObjectDocument>();
//         var active = Substitute.For<StreamInformation>();
//         active.StreamIdentifier = "stream-ABC";
//         active.SnapShotConnectionName = "snapshot-conn";
//         doc.Active.Returns(active);
//         doc.ObjectName.Returns("MyObj");
//         doc.ObjectId.Returns("id-1");
//         return doc;
//     }
// }
