using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Migration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Migration;

public class AzureBlobBackupProviderTests
{
    private readonly BlobServiceClient mockBlobServiceClient;
    private readonly ILogger<AzureBlobBackupProvider> mockLogger;

    public AzureBlobBackupProviderTests()
    {
        mockBlobServiceClient = Substitute.For<BlobServiceClient>();
        mockLogger = Substitute.For<ILogger<AzureBlobBackupProvider>>();
    }

    public class Constructor : AzureBlobBackupProviderTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Act
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_blobServiceClient_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AzureBlobBackupProvider(null!, mockLogger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AzureBlobBackupProvider(mockBlobServiceClient, null!));
        }

        [Fact]
        public void Should_use_default_container_name_when_not_specified()
        {
            // Act
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_use_custom_container_name_when_specified()
        {
            // Act
            var sut = new AzureBlobBackupProvider(
                mockBlobServiceClient,
                mockLogger,
                "custom-backups");

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ProviderNameProperty : AzureBlobBackupProviderTests
    {
        [Fact]
        public void Should_return_azure_blob()
        {
            // Arrange
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Assert
            Assert.Equal("azure-blob", sut.ProviderName);
        }
    }

    public class BackupAsyncMethod : AzureBlobBackupProviderTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_context_is_null()
        {
            // Arrange
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.BackupAsync(null!, null));
        }

        [Fact]
        public async Task Should_create_container_if_not_exists()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockUri = new Uri("https://test.blob.core.windows.net/backup/test.json");

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.Uri.Returns(mockUri);

            var mockDocument = CreateMockObjectDocument("obj-123", "TestObject", 5);
            var context = new BackupContext
            {
                Document = mockDocument,
                Configuration = new BackupConfiguration()
            };

            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            await sut.BackupAsync(context, null);

            // Assert
            await mockContainerClient.Received(1).CreateIfNotExistsAsync(cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_upload_backup_blob()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockUri = new Uri("https://test.blob.core.windows.net/backup/test.json");

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.Uri.Returns(mockUri);

            var mockDocument = CreateMockObjectDocument("obj-123", "TestObject", 5);
            var context = new BackupContext
            {
                Document = mockDocument,
                Configuration = new BackupConfiguration()
            };

            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            await sut.BackupAsync(context, null);

            // Assert
            await mockBlobClient.Received(1).UploadAsync(
                Arg.Any<BinaryData>(),
                overwrite: false,
                cancellationToken: Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_backup_handle_with_correct_properties()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockUri = new Uri("https://test.blob.core.windows.net/backup/test.json");

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.Uri.Returns(mockUri);

            var mockDocument = CreateMockObjectDocument("obj-123", "TestObject", 5);
            var context = new BackupContext
            {
                Document = mockDocument,
                Configuration = new BackupConfiguration()
            };

            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.BackupAsync(context, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("azure-blob", result.ProviderName);
            Assert.Equal("obj-123", result.ObjectId);
            Assert.Equal("TestObject", result.ObjectName);
            Assert.Equal(5, result.StreamVersion);
            Assert.Equal(mockUri.ToString(), result.Location);
        }

        [Fact]
        public async Task Should_include_events_in_backup()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockUri = new Uri("https://test.blob.core.windows.net/backup/test.json");

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.Uri.Returns(mockUri);

            var mockEvent1 = Substitute.For<IEvent>();
            mockEvent1.EventType.Returns("TestEvent1");
            mockEvent1.EventVersion.Returns(1);

            var mockEvent2 = Substitute.For<IEvent>();
            mockEvent2.EventType.Returns("TestEvent2");
            mockEvent2.EventVersion.Returns(1);

            var mockDocument = CreateMockObjectDocument("obj-123", "TestObject", 5);
            var context = new BackupContext
            {
                Document = mockDocument,
                Configuration = new BackupConfiguration(),
                Events = new[] { mockEvent1, mockEvent2 }
            };

            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.BackupAsync(context, null);

            // Assert
            Assert.Equal(2, result.EventCount);
        }

        [Fact]
        public async Task Should_handle_compression_when_enabled()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockUri = new Uri("https://test.blob.core.windows.net/backup/test.json");

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.Uri.Returns(mockUri);

            var mockDocument = CreateMockObjectDocument("obj-123", "TestObject", 5);
            var context = new BackupContext
            {
                Document = mockDocument,
                Configuration = new BackupConfiguration { EnableCompression = true }
            };

            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.BackupAsync(context, null);

            // Assert
            Assert.True(result.Metadata.IsCompressed);
        }

        [Fact]
        public async Task Should_set_metadata_based_on_configuration()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockUri = new Uri("https://test.blob.core.windows.net/backup/test.json");

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockBlobClient.Uri.Returns(mockUri);

            var mockDocument = CreateMockObjectDocument("obj-123", "TestObject", 5);
            var context = new BackupContext
            {
                Document = mockDocument,
                Configuration = new BackupConfiguration
                {
                    IncludeSnapshots = true,
                    IncludeObjectDocument = true,
                    IncludeTerminatedStreams = true,
                    EnableCompression = true
                }
            };

            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.BackupAsync(context, null);

            // Assert
            Assert.True(result.Metadata.IncludesSnapshots);
            Assert.True(result.Metadata.IncludesObjectDocument);
            Assert.True(result.Metadata.IncludesTerminatedStreams);
            Assert.True(result.Metadata.IsCompressed);
        }
    }

    public class RestoreAsyncMethod : AzureBlobBackupProviderTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_handle_is_null()
        {
            // Arrange
            var mockDocument = CreateMockObjectDocument("obj-123", "TestObject", 5);
            var context = new RestoreContext { TargetDocument = mockDocument };
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RestoreAsync(null!, context, null));
        }

        [Fact]
        public async Task Should_throw_ArgumentNullException_when_context_is_null()
        {
            // Arrange
            var handle = CreateMockBackupHandle();
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RestoreAsync(handle, null!, null));
        }

        [Fact]
        public async Task Should_download_backup_blob()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockResponse = Substitute.For<Response<BlobDownloadResult>>();
            var backupData = CreateTestBackupJson();

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            var result = BlobsModelFactory.BlobDownloadResult(content: new BinaryData(backupData));
            mockResponse.Value.Returns(result);
            mockBlobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockResponse));

            var handle = CreateMockBackupHandle(isCompressed: false);
            var mockDocument = CreateMockObjectDocument("obj-123", "TestObject", 5);
            var context = new RestoreContext { TargetDocument = mockDocument };
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            await sut.RestoreAsync(handle, context, null);

            // Assert
            await mockBlobClient.Received(1).DownloadContentAsync(Arg.Any<CancellationToken>());
        }
    }

    public class ValidateBackupAsyncMethod : AzureBlobBackupProviderTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_handle_is_null()
        {
            // Arrange
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ValidateBackupAsync(null!));
        }

        [Fact]
        public async Task Should_return_false_when_blob_does_not_exist()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(false);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var handle = CreateMockBackupHandle();
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.ValidateBackupAsync(handle);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_true_when_backup_is_valid()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();
            var mockResponse = Substitute.For<Response<BlobDownloadResult>>();

            var backupId = Guid.NewGuid();
            var backupData = CreateTestBackupJson(backupId);

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(true);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var result = BlobsModelFactory.BlobDownloadResult(content: new BinaryData(backupData));
            mockResponse.Value.Returns(result);
            mockBlobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockResponse));

            var handle = CreateMockBackupHandle(backupId: backupId, isCompressed: false);
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            var isValid = await sut.ValidateBackupAsync(handle);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task Should_return_false_when_backup_id_does_not_match()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();
            var mockResponse = Substitute.For<Response<BlobDownloadResult>>();

            var backupId = Guid.NewGuid();
            var differentId = Guid.NewGuid();
            var backupData = CreateTestBackupJson(differentId); // Different ID

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(true);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));

            var result = BlobsModelFactory.BlobDownloadResult(content: new BinaryData(backupData));
            mockResponse.Value.Returns(result);
            mockBlobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mockResponse));

            var handle = CreateMockBackupHandle(backupId: backupId, isCompressed: false);
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            var isValid = await sut.ValidateBackupAsync(handle);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task Should_return_false_when_exception_occurs()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();
            var mockExistsResponse = Substitute.For<Response<bool>>();

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);
            mockExistsResponse.Value.Returns(true);
            mockBlobClient.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(mockExistsResponse));
            mockBlobClient.DownloadContentAsync(Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Error"));

            var handle = CreateMockBackupHandle();
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            var result = await sut.ValidateBackupAsync(handle);

            // Assert
            Assert.False(result);
        }
    }

    public class DeleteBackupAsyncMethod : AzureBlobBackupProviderTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_handle_is_null()
        {
            // Arrange
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.DeleteBackupAsync(null!));
        }

        [Fact]
        public async Task Should_delete_backup_blob()
        {
            // Arrange
            var mockContainerClient = Substitute.For<BlobContainerClient>();
            var mockBlobClient = Substitute.For<BlobClient>();

            mockBlobServiceClient.GetBlobContainerClient("migration-backups").Returns(mockContainerClient);
            mockContainerClient.GetBlobClient(Arg.Any<string>()).Returns(mockBlobClient);

            var handle = CreateMockBackupHandle();
            var sut = new AzureBlobBackupProvider(mockBlobServiceClient, mockLogger);

            // Act
            await sut.DeleteBackupAsync(handle);

            // Assert
            await mockBlobClient.Received(1).DeleteIfExistsAsync(cancellationToken: Arg.Any<CancellationToken>());
        }
    }

    public class BlobBackupHandleTests : AzureBlobBackupProviderTests
    {
        [Fact]
        public void Should_create_handle_with_all_properties()
        {
            // Arrange & Act
            var backupId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow;
            var handle = new BlobBackupHandle
            {
                BackupId = backupId,
                CreatedAt = createdAt,
                ProviderName = "azure-blob",
                Location = "https://test.blob.core.windows.net/backup/test.json",
                ObjectId = "obj-123",
                ObjectName = "TestObject",
                StreamVersion = 5,
                EventCount = 10,
                SizeBytes = 1024,
                Metadata = new BackupMetadata
                {
                    IncludesSnapshots = true,
                    IsCompressed = true
                }
            };

            // Assert
            Assert.Equal(backupId, handle.BackupId);
            Assert.Equal(createdAt, handle.CreatedAt);
            Assert.Equal("azure-blob", handle.ProviderName);
            Assert.Equal("https://test.blob.core.windows.net/backup/test.json", handle.Location);
            Assert.Equal("obj-123", handle.ObjectId);
            Assert.Equal("TestObject", handle.ObjectName);
            Assert.Equal(5, handle.StreamVersion);
            Assert.Equal(10, handle.EventCount);
            Assert.Equal(1024, handle.SizeBytes);
            Assert.True(handle.Metadata.IncludesSnapshots);
            Assert.True(handle.Metadata.IsCompressed);
        }
    }

    private static IObjectDocument CreateMockObjectDocument(string objectId, string objectName, int streamVersion)
    {
        var mockDocument = Substitute.For<IObjectDocument>();
        var active = new StreamInformation
        {
            CurrentStreamVersion = streamVersion
        };

        mockDocument.ObjectId.Returns(objectId);
        mockDocument.ObjectName.Returns(objectName);
        mockDocument.Active.Returns(active);

        return mockDocument;
    }

    private static IBackupHandle CreateMockBackupHandle(Guid? backupId = null, bool isCompressed = false)
    {
        var id = backupId ?? Guid.NewGuid();
        return new BlobBackupHandle
        {
            BackupId = id,
            CreatedAt = DateTimeOffset.UtcNow,
            ProviderName = "azure-blob",
            Location = "https://test.blob.core.windows.net/backup/test.json",
            ObjectId = "obj-123",
            ObjectName = "TestObject",
            StreamVersion = 5,
            EventCount = 0,
            SizeBytes = 100,
            Metadata = new BackupMetadata { IsCompressed = isCompressed }
        };
    }

    private static string CreateTestBackupJson(Guid? backupId = null)
    {
        var id = backupId ?? Guid.NewGuid();
        return $@"{{
            ""backupId"": ""{id}"",
            ""createdAt"": ""{DateTimeOffset.UtcNow:O}"",
            ""objectId"": ""obj-123"",
            ""objectName"": ""TestObject"",
            ""streamVersion"": 5,
            ""eventCount"": 0
        }}";
    }
}
