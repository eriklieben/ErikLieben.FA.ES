using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Core;

public class MigrationExecutorTests
{
    private static (MigrationContext context, IDistributedLockProvider lockProvider, ILoggerFactory loggerFactory) CreateDependencies()
    {
        var document = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = "source-stream",
            StreamType = "blob",
            CurrentStreamVersion = 0
        };
        document.Active.Returns(streamInfo);
        document.ObjectId.Returns("test-object-id");
        document.ObjectName.Returns("TestObject");
        document.TerminatedStreams.Returns(new List<TerminatedStream>());

        var dataStore = Substitute.For<IDataStore>();
        var documentStore = Substitute.For<IDocumentStore>();

        var context = new MigrationContext
        {
            MigrationId = Guid.NewGuid(),
            SourceDocument = document,
            SourceStreamIdentifier = "source-stream",
            TargetStreamIdentifier = "target-stream",
            DataStore = dataStore,
            DocumentStore = documentStore,
            Strategy = MigrationStrategy.CopyAndTransform
        };

        var lockProvider = Substitute.For<IDistributedLockProvider>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<MigrationExecutor>().Returns(Substitute.For<ILogger<MigrationExecutor>>());
        loggerFactory.CreateLogger<MigrationProgressTracker>().Returns(Substitute.For<ILogger<MigrationProgressTracker>>());

        return (context, lockProvider, loggerFactory);
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
        public void Should_throw_ArgumentNullException_when_context_is_null()
        {
            // Arrange
            var (_, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MigrationExecutor(null!, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_lock_provider_is_null()
        {
            // Arrange
            var (context, _, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MigrationExecutor(context, null!, loggerFactory));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();

            // Act
            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ExecuteAsyncMethod
    {
        [Fact]
        public async Task Should_execute_dry_run_when_configured()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.IsDryRun = true;
            context.BackupConfig = new BackupConfiguration { Location = "/backups" }; // Required for feasibility

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.IsFeasible);
        }

        [Fact]
        public async Task Should_return_failure_on_exception()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            var lockOptions = new DistributedLockOptions();
            lockOptions.LockTimeout(TimeSpan.FromSeconds(5));
            context.LockOptions = lockOptions;

            // Set up lock provider to return null (failed to acquire)
            lockProvider.AcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns((IDistributedLock?)null);

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.Exception);
        }

        [Fact]
        public async Task Should_acquire_lock_when_configured()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            var lockOptions = new DistributedLockOptions();
            lockOptions.LockTimeout(TimeSpan.FromMinutes(5));
            context.LockOptions = lockOptions;

            var mockLock = Substitute.For<IDistributedLock>();
            mockLock.LockId.Returns("test-lock-id");

            lockProvider.AcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(mockLock);

            // Mock data store to return empty events
            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            await lockProvider.Received(1).AcquireLockAsync(
                Arg.Any<string>(),
                TimeSpan.FromMinutes(5),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_execute_migration_saga_without_lock()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();

            // No lock options, but set up data store
            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_handle_events_with_transformer()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();

            var mockEvent = CreateMockEvent();
            var transformedEvent = CreateMockEvent("TransformedEvent", 2);

            var transformer = Substitute.For<IEventTransformer>();
            transformer.TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
                .Returns(transformedEvent);

            context.Transformer = transformer;

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(new[] { mockEvent }));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            await transformer.Received(1).TransformAsync(mockEvent, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_handle_transformation_failure_with_fail_fast()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();

            var mockEvent = CreateMockEvent();

            var transformer = Substitute.For<IEventTransformer>();
            transformer.TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
                .Returns<IEvent>(x => throw new Exception("Transformation failed"));

            context.Transformer = transformer;
            context.VerificationConfig = new VerificationConfiguration { FailFast = true };

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(new[] { mockEvent }));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.False(result.Success);
        }

        [Fact]
        public async Task Should_skip_event_on_transformation_failure_without_fail_fast()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();

            var mockEvent = CreateMockEvent();

            var transformer = Substitute.For<IEventTransformer>();
            transformer.TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
                .Returns<IEvent>(x => throw new Exception("Transformation failed"));

            context.Transformer = transformer;
            context.VerificationConfig = new VerificationConfiguration { FailFast = false };

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(new[] { mockEvent }));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.Statistics.TransformationFailures);
        }

        [Fact]
        public async Task Should_support_rollback_on_failure()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.SupportsRollback = true;
            context.LockOptions = new DistributedLockOptions();

            // Make lock acquisition fail
            lockProvider.AcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns((IDistributedLock?)null);

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.False(result.Success);
        }

        [Fact]
        public async Task Should_support_backup_configuration()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.BackupConfig = new BackupConfiguration { Location = "/backups" };

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_support_verification_configuration()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.VerificationConfig = new VerificationConfiguration { CompareEventCounts = true };

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_support_book_closing_configuration()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.BookClosingConfig = new BookClosingConfiguration { Reason = "Migration complete" };

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_set_up_heartbeat_when_configured()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            var lockOptions = new DistributedLockOptions();
            lockOptions.LockTimeout(TimeSpan.FromMinutes(5));
            lockOptions.HeartbeatInterval(TimeSpan.FromSeconds(30));
            context.LockOptions = lockOptions;

            var mockLock = Substitute.For<IDistributedLock>();
            mockLock.LockId.Returns("test-lock-id");
            mockLock.RenewAsync(Arg.Any<CancellationToken>()).Returns(true);

            lockProvider.AcquireLockAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(mockLock);

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_dry_run_analyze_source_stream_events()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.IsDryRun = true;

            var events = new[]
            {
                CreateMockEvent("EventA", 1),
                CreateMockEvent("EventA", 2),
                CreateMockEvent("EventB", 3)
            };

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(events));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result.Plan);
            Assert.Equal(3, result.Plan.SourceAnalysis.EventCount);
            Assert.Equal(2, result.Plan.SourceAnalysis.EventTypeDistribution.Count);
            Assert.Equal(2, result.Plan.SourceAnalysis.EventTypeDistribution["EventA"]);
            Assert.Equal(1, result.Plan.SourceAnalysis.EventTypeDistribution["EventB"]);
        }

        [Fact]
        public async Task Should_dry_run_simulate_transformations()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.IsDryRun = true;

            var events = new[]
            {
                CreateMockEvent("TestEvent", 1),
                CreateMockEvent("TestEvent", 2)
            };

            var transformer = Substitute.For<IEventTransformer>();
            var transformedEvent = CreateMockEvent("TransformedEvent", 1);
            transformer.TransformAsync(Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
                .Returns(transformedEvent);

            context.Transformer = transformer;
            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(events));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result.Plan);
            Assert.Equal(2, result.Plan.TransformationSimulation.SampleSize);
            Assert.Equal(2, result.Plan.TransformationSimulation.SuccessfulTransformations);
            Assert.Equal(0, result.Plan.TransformationSimulation.FailedTransformations);
        }

        [Fact]
        public async Task Should_dry_run_record_transformation_failures()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.IsDryRun = true;

            var testEvent = CreateMockEvent("TestEvent", 1);
            var failingEvent = CreateMockEvent("FailingEvent", 2);
            var events = new[] { testEvent, failingEvent };

            var transformer = Substitute.For<IEventTransformer>();
            var transformedEvent = CreateMockEvent("TransformedEvent", 1);

            // Set up transformer - configure before assigning to context
            transformer.TransformAsync(testEvent, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(transformedEvent));
            transformer.TransformAsync(failingEvent, Arg.Any<CancellationToken>())
                .Returns<Task<IEvent>>(x => throw new Exception("Transformation failed"));

            context.Transformer = transformer;
            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(events));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result.Plan);
            Assert.Equal(2, result.Plan.TransformationSimulation.SampleSize);
            Assert.Equal(1, result.Plan.TransformationSimulation.SuccessfulTransformations);
            Assert.Equal(1, result.Plan.TransformationSimulation.FailedTransformations);
            Assert.Single(result.Plan.TransformationSimulation.Failures);
            Assert.Equal("FailingEvent", result.Plan.TransformationSimulation.Failures[0].EventName);
        }

        [Fact]
        public async Task Should_dry_run_check_prerequisites()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.IsDryRun = true;

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result.Plan);
            Assert.Contains(result.Plan.Prerequisites, p => p.Name == "DataStore" && p.IsMet);
            Assert.Contains(result.Plan.Prerequisites, p => p.Name == "DocumentStore" && p.IsMet);
        }

        [Fact]
        public async Task Should_dry_run_identify_missing_backup_risk()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.IsDryRun = true;
            context.BackupConfig = null; // No backup configured

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result.Plan);
            Assert.Contains(result.Plan.Risks, r => r.Category == "Data Safety" && r.Severity == "High");
        }

        [Fact]
        public async Task Should_dry_run_be_feasible_with_backup_configured()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.IsDryRun = true;
            context.BackupConfig = new BackupConfiguration { Location = "/backups" };

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.IsFeasible);
            Assert.DoesNotContain(result.Plan.Risks, r => r.Category == "Data Safety");
        }

        [Fact]
        public async Task Should_execute_backup_when_backup_provider_configured()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.BackupConfig = new BackupConfiguration { Location = "/backups" };

            var backupProvider = Substitute.For<ErikLieben.FA.ES.EventStreamManagement.Backup.IBackupProvider>();
            var backupHandle = Substitute.For<ErikLieben.FA.ES.EventStreamManagement.Backup.IBackupHandle>();
            backupHandle.BackupId.Returns(Guid.NewGuid());

            backupProvider.BackupAsync(
                Arg.Any<ErikLieben.FA.ES.EventStreamManagement.Backup.BackupContext>(),
                Arg.Any<IProgress<ErikLieben.FA.ES.EventStreamManagement.Backup.BackupProgress>?>(),
                Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(backupHandle));

            context.BackupProvider = backupProvider;

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            await backupProvider.Received(1).BackupAsync(
                Arg.Any<ErikLieben.FA.ES.EventStreamManagement.Backup.BackupContext>(),
                Arg.Any<IProgress<ErikLieben.FA.ES.EventStreamManagement.Backup.BackupProgress>?>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_execute_backup_when_backup_provider_not_configured()
        {
            // Arrange
            var (context, lockProvider, loggerFactory) = CreateDependencies();
            context.BackupConfig = new BackupConfiguration { Location = "/backups" };
            // BackupProvider is NOT set

            context.DataStore!.ReadAsync(Arg.Any<IObjectDocument>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>())
                .Returns(Task.FromResult<IEnumerable<IEvent>?>(Array.Empty<IEvent>()));

            var sut = new MigrationExecutor(context, lockProvider, loggerFactory);

            // Act
            var result = await sut.ExecuteAsync(CancellationToken.None);

            // Assert - migration should succeed even without backup provider (backup is skipped)
            Assert.True(result.Success);
        }
    }
}
