using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Backup;

public class BackupRestoreServiceTests
{
    private static (
        IBackupProvider backupProvider,
        IDocumentStore documentStore,
        IDataStore dataStore,
        ILogger<BackupRestoreService> logger,
        IBackupRegistry? backupRegistry) CreateDependencies(bool includeRegistry = false)
    {
        var backupProvider = Substitute.For<IBackupProvider>();
        var documentStore = Substitute.For<IDocumentStore>();
        var dataStore = Substitute.For<IDataStore>();
        var logger = Substitute.For<ILogger<BackupRestoreService>>();
        var backupRegistry = includeRegistry ? Substitute.For<IBackupRegistry>() : null;

        return (backupProvider, documentStore, dataStore, logger, backupRegistry);
    }

    private static IObjectDocument CreateMockDocument(
        string objectId = "test-id",
        string objectName = "TestObject",
        int version = 5)
    {
        var document = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = $"stream-{objectId}",
            StreamType = "blob",
            CurrentStreamVersion = version
        };
        document.Active.Returns(streamInfo);
        document.ObjectId.Returns(objectId);
        document.ObjectName.Returns(objectName);
        return document;
    }

    private static IBackupHandle CreateMockBackupHandle(
        Guid? backupId = null,
        string objectId = "test-id",
        string objectName = "TestObject",
        int eventCount = 10)
    {
        var handle = Substitute.For<IBackupHandle>();
        handle.BackupId.Returns(backupId ?? Guid.NewGuid());
        handle.ObjectId.Returns(objectId);
        handle.ObjectName.Returns(objectName);
        handle.EventCount.Returns(eventCount);
        handle.CreatedAt.Returns(DateTimeOffset.UtcNow);
        handle.Metadata.Returns(new BackupMetadata
        {
            IncludesObjectDocument = true,
            IsCompressed = true
        });
        return handle;
    }

    private static IEvent CreateMockEvent(string eventType = "TestEvent", int version = 1)
    {
        var mockEvent = Substitute.For<IEvent>();
        mockEvent.EventType.Returns(eventType);
        mockEvent.EventVersion.Returns(version);
        return mockEvent;
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_backupProvider_is_null()
        {
            // Arrange
            var (_, documentStore, dataStore, logger, _) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BackupRestoreService(null!, documentStore, dataStore, logger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_documentStore_is_null()
        {
            // Arrange
            var (backupProvider, _, dataStore, logger, _) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BackupRestoreService(backupProvider, null!, dataStore, logger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_dataStore_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, _, logger, _) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BackupRestoreService(backupProvider, documentStore, null!, logger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, _, _) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BackupRestoreService(backupProvider, documentStore, dataStore, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();

            // Act
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_optional_registry()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);

            // Act
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class BackupStreamAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_objectName_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.BackupStreamAsync(null!, "object-id"));
        }

        [Fact]
        public async Task Should_throw_ArgumentException_when_objectName_is_empty()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.BackupStreamAsync("", "object-id"));
        }

        [Fact]
        public async Task Should_throw_ArgumentNullException_when_objectId_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.BackupStreamAsync("TestObject", null!));
        }

        [Fact]
        public async Task Should_get_document_from_store()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var document = CreateMockDocument();
            var handle = CreateMockBackupHandle();

            documentStore.GetAsync("TestObject", "test-id", null).Returns(document);
            dataStore.ReadAsync(document, 0, null, null).Returns(new List<IEvent>());
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(handle);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.BackupStreamAsync("TestObject", "test-id");

            // Assert
            await documentStore.Received(1).GetAsync("TestObject", "test-id", null);
        }

        [Fact]
        public async Task Should_read_events_from_data_store()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var document = CreateMockDocument();
            var handle = CreateMockBackupHandle();
            var events = new List<IEvent> { CreateMockEvent(), CreateMockEvent() };

            documentStore.GetAsync("TestObject", "test-id", null).Returns(document);
            dataStore.ReadAsync(document, 0, null, null).Returns(events);
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(handle);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.BackupStreamAsync("TestObject", "test-id");

            // Assert
            await dataStore.Received(1).ReadAsync(document, 0, null, null);
        }

        [Fact]
        public async Task Should_call_backup_provider_with_correct_context()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var document = CreateMockDocument();
            var handle = CreateMockBackupHandle();
            var events = new List<IEvent> { CreateMockEvent() };

            documentStore.GetAsync("TestObject", "test-id", null).Returns(document);
            dataStore.ReadAsync(document, 0, null, null).Returns(events);
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(handle);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.BackupStreamAsync("TestObject", "test-id");

            // Assert
            await backupProvider.Received(1).BackupAsync(
                Arg.Is<BackupContext>(ctx =>
                    ctx.Document == document &&
                    ctx.Events!.Count() == 1),
                Arg.Any<IProgress<BackupProgress>?>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_backup_handle()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var document = CreateMockDocument();
            var expectedHandle = CreateMockBackupHandle();

            documentStore.GetAsync("TestObject", "test-id", null).Returns(document);
            dataStore.ReadAsync(document, 0, null, null).Returns(new List<IEvent>());
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(expectedHandle);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            var result = await sut.BackupStreamAsync("TestObject", "test-id");

            // Assert
            Assert.Same(expectedHandle, result);
        }

        [Fact]
        public async Task Should_use_default_options_when_not_provided()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var document = CreateMockDocument();
            var handle = CreateMockBackupHandle();

            documentStore.GetAsync("TestObject", "test-id", null).Returns(document);
            dataStore.ReadAsync(document, 0, null, null).Returns(new List<IEvent>());
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(handle);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.BackupStreamAsync("TestObject", "test-id");

            // Assert
            await backupProvider.Received(1).BackupAsync(
                Arg.Is<BackupContext>(ctx =>
                    ctx.Configuration.EnableCompression == BackupOptions.Default.EnableCompression &&
                    ctx.Configuration.IncludeObjectDocument == BackupOptions.Default.IncludeObjectDocument),
                Arg.Any<IProgress<BackupProgress>?>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_register_backup_when_registry_is_configured()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);
            var document = CreateMockDocument();
            var handle = CreateMockBackupHandle();

            documentStore.GetAsync("TestObject", "test-id", null).Returns(document);
            dataStore.ReadAsync(document, 0, null, null).Returns(new List<IEvent>());
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(handle);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Act
            await sut.BackupStreamAsync("TestObject", "test-id");

            // Assert
            await backupRegistry!.Received(1).RegisterBackupAsync(
                handle,
                Arg.Any<BackupOptions>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class BackupDocumentAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_document_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.BackupDocumentAsync(null!));
        }

        [Fact]
        public async Task Should_read_events_from_data_store()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var document = CreateMockDocument();
            var handle = CreateMockBackupHandle();

            dataStore.ReadAsync(document, 0, null, null).Returns(new List<IEvent>());
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(handle);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.BackupDocumentAsync(document);

            // Assert
            await dataStore.Received(1).ReadAsync(document, 0, null, null);
        }

        [Fact]
        public async Task Should_report_progress()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var document = CreateMockDocument();
            var handle = CreateMockBackupHandle();
            var events = new List<IEvent> { CreateMockEvent(), CreateMockEvent() };
            var progressReported = false;

            dataStore.ReadAsync(document, 0, null, null).Returns(events);
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(handle);

            var progress = new Progress<BackupProgress>(p =>
            {
                progressReported = true;
                Assert.Equal(2, p.TotalEvents);
            });

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.BackupDocumentAsync(document, progress: progress);

            // Assert - allow time for progress reporting
            await Task.Delay(100);
            Assert.True(progressReported);
        }
    }

    public class RestoreStreamAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_handle_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RestoreStreamAsync(null!));
        }

        [Fact]
        public async Task Should_validate_backup_when_option_is_set()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();
            var document = CreateMockDocument(version: -1);

            backupProvider.ValidateBackupAsync(handle, Arg.Any<CancellationToken>()).Returns(true);
            documentStore.CreateAsync("TestObject", "test-id", null).Returns(document);

            var options = new RestoreOptions { ValidateBeforeRestore = true };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.RestoreStreamAsync(handle, options);

            // Assert
            await backupProvider.Received(1).ValidateBackupAsync(handle, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_when_validation_fails()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();

            backupProvider.ValidateBackupAsync(handle, Arg.Any<CancellationToken>()).Returns(false);

            var options = new RestoreOptions { ValidateBeforeRestore = true };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.RestoreStreamAsync(handle, options));
            Assert.Contains("failed validation", ex.Message);
        }

        [Fact]
        public async Task Should_skip_validation_when_option_is_false()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();
            var document = CreateMockDocument(version: -1);

            documentStore.CreateAsync("TestObject", "test-id", null).Returns(document);

            var options = new RestoreOptions { ValidateBeforeRestore = false };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.RestoreStreamAsync(handle, options);

            // Assert
            await backupProvider.DidNotReceive().ValidateBackupAsync(Arg.Any<IBackupHandle>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_when_stream_exists_and_overwrite_is_false()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();
            var document = CreateMockDocument(version: 5); // Existing stream with version 5

            documentStore.CreateAsync("TestObject", "test-id", null).Returns(document);

            var options = new RestoreOptions { ValidateBeforeRestore = false, Overwrite = false };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.RestoreStreamAsync(handle, options));
            Assert.Contains("already exists", ex.Message);
        }

        [Fact]
        public async Task Should_restore_when_stream_exists_and_overwrite_is_true()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();
            var document = CreateMockDocument(version: 5);

            documentStore.CreateAsync("TestObject", "test-id", null).Returns(document);

            var options = new RestoreOptions { ValidateBeforeRestore = false, Overwrite = true };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.RestoreStreamAsync(handle, options);

            // Assert
            await backupProvider.Received(1).RestoreAsync(
                handle,
                Arg.Any<RestoreContext>(),
                Arg.Any<IProgress<RestoreProgress>?>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_call_backup_provider_restore()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();
            var document = CreateMockDocument(version: -1);

            backupProvider.ValidateBackupAsync(handle, Arg.Any<CancellationToken>()).Returns(true);
            documentStore.CreateAsync("TestObject", "test-id", null).Returns(document);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.RestoreStreamAsync(handle);

            // Assert
            await backupProvider.Received(1).RestoreAsync(
                handle,
                Arg.Is<RestoreContext>(ctx => ctx.TargetDocument == document),
                Arg.Any<IProgress<RestoreProgress>?>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class RestoreToNewStreamAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_handle_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RestoreToNewStreamAsync(null!, "new-id"));
        }

        [Fact]
        public async Task Should_throw_ArgumentNullException_when_targetObjectId_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RestoreToNewStreamAsync(handle, null!));
        }

        [Fact]
        public async Task Should_create_document_with_new_objectId()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();
            var newDocument = CreateMockDocument(objectId: "new-id", version: -1);

            documentStore.CreateAsync("TestObject", "new-id", null).Returns(newDocument);

            var options = new RestoreOptions { ValidateBeforeRestore = false };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.RestoreToNewStreamAsync(handle, "new-id", options);

            // Assert
            await documentStore.Received(1).CreateAsync("TestObject", "new-id", null);
        }

        [Fact]
        public async Task Should_restore_to_new_document()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();
            var newDocument = CreateMockDocument(objectId: "new-id", version: -1);

            documentStore.CreateAsync("TestObject", "new-id", null).Returns(newDocument);

            var options = new RestoreOptions { ValidateBeforeRestore = false };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.RestoreToNewStreamAsync(handle, "new-id", options);

            // Assert
            await backupProvider.Received(1).RestoreAsync(
                handle,
                Arg.Is<RestoreContext>(ctx => ctx.TargetDocument == newDocument),
                Arg.Any<IProgress<RestoreProgress>?>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class BackupManyAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_objectIds_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.BackupManyAsync(null!, "TestObject"));
        }

        [Fact]
        public async Task Should_backup_all_streams()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var objectIds = new[] { "id-1", "id-2", "id-3" };

            foreach (var id in objectIds)
            {
                var doc = CreateMockDocument(objectId: id);
                documentStore.GetAsync("TestObject", id, null).Returns(doc);
                dataStore.ReadAsync(doc, 0, null, null).Returns(new List<IEvent>());
            }

            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(ci => CreateMockBackupHandle(objectId: ci.Arg<BackupContext>().Document.ObjectId));

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            var result = await sut.BackupManyAsync(objectIds, "TestObject");

            // Assert
            Assert.Equal(3, result.TotalProcessed);
            Assert.Equal(3, result.SuccessCount);
            Assert.Equal(0, result.FailureCount);
            Assert.True(result.IsFullySuccessful);
        }

        [Fact]
        public async Task Should_continue_on_error_when_configured()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var objectIds = new[] { "id-1", "id-2", "id-3" };

            // First succeeds
            var doc1 = CreateMockDocument(objectId: "id-1");
            documentStore.GetAsync("TestObject", "id-1", null).Returns(doc1);
            dataStore.ReadAsync(doc1, 0, null, null).Returns(new List<IEvent>());

            // Second fails
            documentStore.GetAsync("TestObject", "id-2", null).ThrowsAsync(new Exception("Test error"));

            // Third succeeds
            var doc3 = CreateMockDocument(objectId: "id-3");
            documentStore.GetAsync("TestObject", "id-3", null).Returns(doc3);
            dataStore.ReadAsync(doc3, 0, null, null).Returns(new List<IEvent>());

            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(ci => CreateMockBackupHandle(objectId: ci.Arg<BackupContext>().Document.ObjectId));

            var options = new BulkBackupOptions { ContinueOnError = true };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            var result = await sut.BackupManyAsync(objectIds, "TestObject", options);

            // Assert
            Assert.Equal(3, result.TotalProcessed);
            Assert.Equal(2, result.SuccessCount);
            Assert.Equal(1, result.FailureCount);
            Assert.True(result.IsPartialSuccess);
            Assert.Single(result.FailedBackups);
            Assert.Equal("id-2", result.FailedBackups[0].ObjectId);
        }

        [Fact]
        public async Task Should_report_progress()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var objectIds = new[] { "id-1", "id-2" };
            var progressReports = new List<BulkBackupProgress>();

            foreach (var id in objectIds)
            {
                var doc = CreateMockDocument(objectId: id);
                documentStore.GetAsync("TestObject", id, null).Returns(doc);
                dataStore.ReadAsync(doc, 0, null, null).Returns(new List<IEvent>());
            }

            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(ci => CreateMockBackupHandle(objectId: ci.Arg<BackupContext>().Document.ObjectId));

            var options = new BulkBackupOptions
            {
                MaxConcurrency = 1, // Serialize for predictable progress
                OnProgress = p => progressReports.Add(p)
            };

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.BackupManyAsync(objectIds, "TestObject", options);

            // Assert
            Assert.Equal(2, progressReports.Count);
            Assert.Equal(2, progressReports.Last().TotalStreams);
            Assert.Equal(2, progressReports.Last().ProcessedStreams);
        }

        [Fact]
        public async Task Should_track_elapsed_time()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var objectIds = new[] { "id-1" };
            var doc = CreateMockDocument(objectId: "id-1");
            var handle = CreateMockBackupHandle(objectId: "id-1");

            documentStore.GetAsync("TestObject", "id-1", null).Returns(doc);
            dataStore.ReadAsync(doc, 0, null, null).Returns(new List<IEvent>());
            backupProvider.BackupAsync(Arg.Any<BackupContext>(), Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(handle));

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            var result = await sut.BackupManyAsync(objectIds, "TestObject");

            // Assert
            Assert.True(result.ElapsedTime > TimeSpan.Zero);
        }
    }

    public class RestoreManyAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_handles_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.RestoreManyAsync(null!));
        }

        [Fact]
        public async Task Should_restore_all_backups()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handles = new[]
            {
                CreateMockBackupHandle(backupId: Guid.NewGuid(), objectId: "id-1"),
                CreateMockBackupHandle(backupId: Guid.NewGuid(), objectId: "id-2")
            };

            foreach (var handle in handles)
            {
                var doc = CreateMockDocument(objectId: handle.ObjectId, version: -1);
                documentStore.CreateAsync(handle.ObjectName, handle.ObjectId, null).Returns(doc);
            }

            var options = new BulkRestoreOptions { ValidateBeforeRestore = false };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            var result = await sut.RestoreManyAsync(handles, options);

            // Assert
            Assert.Equal(2, result.TotalProcessed);
            Assert.Equal(2, result.SuccessCount);
            Assert.Equal(0, result.FailureCount);
            Assert.True(result.IsFullySuccessful);
        }

        [Fact]
        public async Task Should_continue_on_error_when_configured()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handles = new[]
            {
                CreateMockBackupHandle(backupId: Guid.NewGuid(), objectId: "id-1"),
                CreateMockBackupHandle(backupId: Guid.NewGuid(), objectId: "id-2")
            };

            // First succeeds
            var doc1 = CreateMockDocument(objectId: "id-1", version: -1);
            documentStore.CreateAsync("TestObject", "id-1", null).Returns(doc1);

            // Second fails
            documentStore.CreateAsync("TestObject", "id-2", null).ThrowsAsync(new Exception("Test error"));

            var options = new BulkRestoreOptions { ValidateBeforeRestore = false, ContinueOnError = true };
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            var result = await sut.RestoreManyAsync(handles, options);

            // Assert
            Assert.Equal(2, result.TotalProcessed);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(1, result.FailureCount);
            Assert.True(result.IsPartialSuccess);
        }
    }

    public class ListBackupsAsyncMethod
    {
        [Fact]
        public async Task Should_throw_when_registry_is_not_configured()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies(includeRegistry: false);
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.ListBackupsAsync());
            Assert.Contains("registry is not configured", ex.Message);
        }

        [Fact]
        public async Task Should_query_registry_when_configured()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);
            var expectedBackups = new[] { CreateMockBackupHandle(), CreateMockBackupHandle() };

            backupRegistry!.QueryBackupsAsync(Arg.Any<BackupQuery?>(), Arg.Any<CancellationToken>())
                .Returns(expectedBackups);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Act
            var result = await sut.ListBackupsAsync();

            // Assert
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task Should_pass_query_to_registry()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);
            var query = new BackupQuery { ObjectName = "TestObject" };

            backupRegistry!.QueryBackupsAsync(Arg.Any<BackupQuery?>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<IBackupHandle>());

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Act
            await sut.ListBackupsAsync(query);

            // Assert
            await backupRegistry.Received(1).QueryBackupsAsync(query, Arg.Any<CancellationToken>());
        }
    }

    public class GetBackupAsyncMethod
    {
        [Fact]
        public async Task Should_throw_when_registry_is_not_configured()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies(includeRegistry: false);
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetBackupAsync(Guid.NewGuid()));
            Assert.Contains("registry is not configured", ex.Message);
        }

        [Fact]
        public async Task Should_query_registry_by_id()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);
            var backupId = Guid.NewGuid();
            var expectedHandle = CreateMockBackupHandle(backupId: backupId);

            backupRegistry!.GetBackupAsync(backupId, Arg.Any<CancellationToken>())
                .Returns(expectedHandle);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Act
            var result = await sut.GetBackupAsync(backupId);

            // Assert
            Assert.Same(expectedHandle, result);
        }
    }

    public class ValidateBackupAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_handle_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.ValidateBackupAsync(null!));
        }

        [Fact]
        public async Task Should_delegate_to_backup_provider()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();

            backupProvider.ValidateBackupAsync(handle, Arg.Any<CancellationToken>()).Returns(true);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            var result = await sut.ValidateBackupAsync(handle);

            // Assert
            Assert.True(result);
            await backupProvider.Received(1).ValidateBackupAsync(handle, Arg.Any<CancellationToken>());
        }
    }

    public class DeleteBackupAsyncMethod
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_handle_is_null()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.DeleteBackupAsync(null!));
        }

        [Fact]
        public async Task Should_delete_from_provider()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies();
            var handle = CreateMockBackupHandle();

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act
            await sut.DeleteBackupAsync(handle);

            // Assert
            await backupProvider.Received(1).DeleteBackupAsync(handle, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_unregister_from_registry_when_configured()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);
            var handle = CreateMockBackupHandle();

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Act
            await sut.DeleteBackupAsync(handle);

            // Assert
            await backupRegistry!.Received(1).UnregisterBackupAsync(handle.BackupId, Arg.Any<CancellationToken>());
        }
    }

    public class CleanupExpiredBackupsAsyncMethod
    {
        [Fact]
        public async Task Should_throw_when_registry_is_not_configured()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, _) = CreateDependencies(includeRegistry: false);
            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.CleanupExpiredBackupsAsync());
            Assert.Contains("registry is not configured", ex.Message);
        }

        [Fact]
        public async Task Should_return_zero_when_no_expired_backups()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);

            backupRegistry!.GetExpiredBackupsAsync(Arg.Any<CancellationToken>())
                .Returns(Array.Empty<IBackupHandle>());

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Act
            var result = await sut.CleanupExpiredBackupsAsync();

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task Should_delete_expired_backups()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);
            var expiredBackups = new[]
            {
                CreateMockBackupHandle(backupId: Guid.NewGuid()),
                CreateMockBackupHandle(backupId: Guid.NewGuid())
            };

            backupRegistry!.GetExpiredBackupsAsync(Arg.Any<CancellationToken>())
                .Returns(expiredBackups);

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Act
            var result = await sut.CleanupExpiredBackupsAsync();

            // Assert
            Assert.Equal(2, result);
            await backupProvider.Received(2).DeleteBackupAsync(Arg.Any<IBackupHandle>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_continue_cleanup_on_individual_delete_failure()
        {
            // Arrange
            var (backupProvider, documentStore, dataStore, logger, backupRegistry) = CreateDependencies(includeRegistry: true);
            var backup1 = CreateMockBackupHandle(backupId: Guid.NewGuid());
            var backup2 = CreateMockBackupHandle(backupId: Guid.NewGuid());
            var expiredBackups = new[] { backup1, backup2 };

            backupRegistry!.GetExpiredBackupsAsync(Arg.Any<CancellationToken>())
                .Returns(expiredBackups);

            // First delete fails, second succeeds
            backupProvider.DeleteBackupAsync(backup1, Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Delete failed"));

            var sut = new BackupRestoreService(backupProvider, documentStore, dataStore, logger, backupRegistry);

            // Act
            var result = await sut.CleanupExpiredBackupsAsync();

            // Assert - should still delete the second one
            Assert.Equal(1, result);
            await backupProvider.Received(2).DeleteBackupAsync(Arg.Any<IBackupHandle>(), Arg.Any<CancellationToken>());
        }
    }
}
