using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using ErikLieben.FA.ES.EventStreamManagement.Verification;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Core;

public class MigrationBuilderTests
{
    private static (IObjectDocument document, IDataStore dataStore, IDocumentStore documentStore,
        IDistributedLockProvider lockProvider, ILoggerFactory loggerFactory) CreateDependencies()
    {
        var document = Substitute.For<IObjectDocument>();
        var streamInfo = Substitute.For<StreamInformation>();
        streamInfo.StreamIdentifier = "source-stream";
        document.Active.Returns(streamInfo);

        var dataStore = Substitute.For<IDataStore>();
        var documentStore = Substitute.For<IDocumentStore>();
        var lockProvider = Substitute.For<IDistributedLockProvider>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<MigrationBuilder>().Returns(Substitute.For<ILogger<MigrationBuilder>>());
        loggerFactory.CreateLogger<MigrationExecutor>().Returns(Substitute.For<ILogger<MigrationExecutor>>());
        loggerFactory.CreateLogger<MigrationProgressTracker>().Returns(Substitute.For<ILogger<MigrationProgressTracker>>());
        loggerFactory.CreateLogger<TransformationPipeline>().Returns(Substitute.For<ILogger<TransformationPipeline>>());

        return (document, dataStore, documentStore, lockProvider, loggerFactory);
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_document_is_null()
        {
            // Arrange
            var (_, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MigrationBuilder(null!, dataStore, documentStore, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_data_store_is_null()
        {
            // Arrange
            var (document, _, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MigrationBuilder(document, null!, documentStore, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_document_store_is_null()
        {
            // Arrange
            var (document, dataStore, _, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MigrationBuilder(document, dataStore, null!, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_lock_provider_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, _, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MigrationBuilder(document, dataStore, documentStore, null!, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_factory_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, _) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MigrationBuilder(document, dataStore, documentStore, lockProvider, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class CopyToNewStreamMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_stream_identifier_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CopyToNewStream(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.CopyToNewStream("target-stream");

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithTransformationMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_transformer_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithTransformation(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);
            var transformer = Substitute.For<IEventTransformer>();

            // Act
            var result = sut.WithTransformation(transformer);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithPipelineMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_configure_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithPipeline(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithPipeline(builder => { });

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithDistributedLockMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_configure_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithDistributedLock(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithDistributedLock(options => { });

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithBackupMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_configure_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithBackup(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithBackup(builder => { });

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithBookClosingMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_configure_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithBookClosing(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithBookClosing(builder => { });

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithVerificationMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_configure_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithVerification(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithVerification(builder => { });

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class DryRunMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.DryRun();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithProgressMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_configure_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithProgress(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithProgress(builder => { });

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithPauseSupportMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithPauseSupport();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithRollbackSupportMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithRollbackSupport();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class FromDryRunPlanMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_plan_is_null()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.FromDryRunPlan(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);
            var plan = Substitute.For<IMigrationPlan>();

            // Act
            var result = sut.FromDryRunPlan(plan);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class ExecuteAsyncMethod
    {
        [Fact]
        public async Task Should_throw_InvalidOperationException_when_target_stream_not_set()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync());
        }

        [Fact]
        public async Task Should_throw_InvalidOperationException_when_source_equals_target()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory);
            sut.CopyToNewStream("source-stream"); // Same as source

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync());
        }
    }

    public class FluentChainingTests
    {
        [Fact]
        public void Should_support_full_fluent_configuration()
        {
            // Arrange
            var (document, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var transformer = Substitute.For<IEventTransformer>();

            // Act
            var sut = new MigrationBuilder(document, dataStore, documentStore, lockProvider, loggerFactory)
                .CopyToNewStream("target-stream")
                .WithTransformation(transformer)
                .WithDistributedLock(options => options.LockTimeout(TimeSpan.FromHours(1)))
                .WithBackup(builder => builder.ToLocation("/backups"))
                .WithBookClosing(builder => builder.Reason("Migration complete"))
                .WithVerification(builder => builder.CompareEventCounts())
                .WithProgress(builder => builder.EnableLogging())
                .WithPauseSupport()
                .WithRollbackSupport()
                .DryRun();

            // Assert
            Assert.NotNull(sut);
        }
    }
}
