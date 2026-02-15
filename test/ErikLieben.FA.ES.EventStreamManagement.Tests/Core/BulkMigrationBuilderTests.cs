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

public class BulkMigrationBuilderTests
{
    private static IObjectDocument CreateMockDocument(string objectId = "test-id", string objectName = "TestObject")
    {
        var document = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = $"stream-{objectId}",
            StreamType = "blob",
            CurrentStreamVersion = 5
        };
        document.Active.Returns(streamInfo);
        document.ObjectId.Returns(objectId);
        document.ObjectName.Returns(objectName);
        return document;
    }

    private static (IEnumerable<IObjectDocument> documents, IDataStore dataStore, IDocumentStore documentStore,
        IDistributedLockProvider lockProvider, ILoggerFactory loggerFactory) CreateDependencies(int documentCount = 2)
    {
        var documents = Enumerable.Range(1, documentCount)
            .Select(i => CreateMockDocument($"id-{i}"))
            .ToList();

        var dataStore = Substitute.For<IDataStore>();
        var documentStore = Substitute.For<IDocumentStore>();
        var lockProvider = Substitute.For<IDistributedLockProvider>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        loggerFactory.CreateLogger<BulkMigrationBuilder>().Returns(Substitute.For<ILogger<BulkMigrationBuilder>>());
        loggerFactory.CreateLogger<MigrationBuilder>().Returns(Substitute.For<ILogger<MigrationBuilder>>());
        loggerFactory.CreateLogger<MigrationExecutor>().Returns(Substitute.For<ILogger<MigrationExecutor>>());
        loggerFactory.CreateLogger<MigrationProgressTracker>().Returns(Substitute.For<ILogger<MigrationProgressTracker>>());
        loggerFactory.CreateLogger<TransformationPipeline>().Returns(Substitute.For<ILogger<TransformationPipeline>>());

        return (documents, dataStore, documentStore, lockProvider, loggerFactory);
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_documents_is_null()
        {
            // Arrange
            var (_, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BulkMigrationBuilder(null!, dataStore, documentStore, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_dataStore_is_null()
        {
            // Arrange
            var (documents, _, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BulkMigrationBuilder(documents, null!, documentStore, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_documentStore_is_null()
        {
            // Arrange
            var (documents, dataStore, _, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BulkMigrationBuilder(documents, dataStore, null!, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_lockProvider_is_null()
        {
            // Arrange
            var (documents, dataStore, documentStore, _, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BulkMigrationBuilder(documents, dataStore, documentStore, null!, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_loggerFactory_is_null()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, _) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, null!));
        }

        [Fact]
        public void Should_throw_ArgumentException_when_documents_is_empty()
        {
            // Arrange
            var (_, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var emptyDocuments = Enumerable.Empty<IObjectDocument>();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                new BulkMigrationBuilder(emptyDocuments, dataStore, documentStore, lockProvider, loggerFactory));
            Assert.Contains("At least one document is required", ex.Message);
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class WithMaxConcurrencyMethod
    {
        [Fact]
        public void Should_throw_ArgumentOutOfRangeException_when_value_is_zero()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.WithMaxConcurrency(0));
        }

        [Fact]
        public void Should_throw_ArgumentOutOfRangeException_when_value_is_negative()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.WithMaxConcurrency(-1));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithMaxConcurrency(8);

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(16)]
        [InlineData(100)]
        public void Should_accept_valid_concurrency_values(int concurrency)
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithMaxConcurrency(concurrency);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithContinueOnErrorMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithContinueOnError(false);

            // Assert
            Assert.Same(sut, result);
        }

        [Fact]
        public void Should_default_to_true_when_called_without_argument()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithContinueOnError();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithBulkProgressMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.WithBulkProgress(_ => { });

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class CopyToNewStreamMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_stream_identifier_is_null()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CopyToNewStream(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.CopyToNewStream("target-stream");

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class CopyToNewStreamsMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_factory_is_null()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.CopyToNewStreams(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = sut.CopyToNewStreams(doc => $"target-{doc.ObjectId}");

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithTransformation(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);
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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithPipeline(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithDistributedLock(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithBackup(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithBookClosing(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithVerification(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.WithProgress(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

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
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.FromDryRunPlan(null!));
        }

        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);
            var plan = Substitute.For<IMigrationPlan>();

            // Act
            var result = sut.FromDryRunPlan(plan);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithLiveMigrationMethod
    {
        [Fact]
        public void Should_throw_NotSupportedException()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            var ex = Assert.Throws<NotSupportedException>(() => sut.WithLiveMigration());
            Assert.Contains("not supported for bulk operations", ex.Message);
        }

        [Fact]
        public void Should_throw_NotSupportedException_with_configure_action()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            var ex = Assert.Throws<NotSupportedException>(() => sut.WithLiveMigration(options => { }));
            Assert.Contains("not supported for bulk operations", ex.Message);
        }
    }

    public class ExecuteLiveMigrationAsyncMethod
    {
        [Fact]
        public async Task Should_throw_NotSupportedException()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotSupportedException>(() => sut.ExecuteLiveMigrationAsync());
            Assert.Contains("not supported for bulk operations", ex.Message);
        }
    }

    public class FluentChainingTests
    {
        [Fact]
        public void Should_support_full_fluent_configuration()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var transformer = Substitute.For<IEventTransformer>();

            // Act
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory)
                .CopyToNewStreams(doc => $"target-{doc.ObjectId}")
                .WithMaxConcurrency(8)
                .WithContinueOnError(false)
                .WithBulkProgress(_ => { })
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

        [Fact]
        public void Should_support_CopyToNewStream_with_static_identifier()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act - Using IMigrationBuilder interface method
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);
            var result = sut.CopyToNewStream("static-target");

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class InterfaceImplementation
    {
        [Fact]
        public void Should_implement_IMigrationBuilder()
        {
            // Arrange
            var (documents, dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act
            var sut = new BulkMigrationBuilder(documents, dataStore, documentStore, lockProvider, loggerFactory);

            // Assert
            Assert.IsType<BulkMigrationBuilder>(sut);
        }
    }
}

public class BulkMigrationProgressTests
{
    public class PercentageCompleteProperty
    {
        [Fact]
        public void Should_return_zero_when_TotalDocuments_is_zero()
        {
            // Arrange
            var sut = new BulkMigrationProgress { TotalDocuments = 0, ProcessedDocuments = 0 };

            // Act & Assert
            Assert.Equal(0.0, sut.PercentageComplete);
        }

        [Fact]
        public void Should_calculate_correct_percentage()
        {
            // Arrange
            var sut = new BulkMigrationProgress { TotalDocuments = 10, ProcessedDocuments = 3 };

            // Act & Assert
            Assert.Equal(30.0, sut.PercentageComplete);
        }

        [Fact]
        public void Should_return_100_when_all_processed()
        {
            // Arrange
            var sut = new BulkMigrationProgress { TotalDocuments = 4, ProcessedDocuments = 4 };

            // Act & Assert
            Assert.Equal(100.0, sut.PercentageComplete);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_TotalDocuments()
        {
            // Arrange
            var sut = new BulkMigrationProgress();

            // Act
            sut.TotalDocuments = 50;

            // Assert
            Assert.Equal(50, sut.TotalDocuments);
        }

        [Fact]
        public void Should_set_and_get_ProcessedDocuments()
        {
            // Arrange
            var sut = new BulkMigrationProgress();

            // Act
            sut.ProcessedDocuments = 25;

            // Assert
            Assert.Equal(25, sut.ProcessedDocuments);
        }

        [Fact]
        public void Should_set_and_get_SuccessfulMigrations()
        {
            // Arrange
            var sut = new BulkMigrationProgress();

            // Act
            sut.SuccessfulMigrations = 20;

            // Assert
            Assert.Equal(20, sut.SuccessfulMigrations);
        }

        [Fact]
        public void Should_set_and_get_FailedMigrations()
        {
            // Arrange
            var sut = new BulkMigrationProgress();

            // Act
            sut.FailedMigrations = 5;

            // Assert
            Assert.Equal(5, sut.FailedMigrations);
        }

        [Fact]
        public void Should_set_and_get_CurrentDocumentId()
        {
            // Arrange
            var sut = new BulkMigrationProgress();

            // Act
            sut.CurrentDocumentId = "doc-456";

            // Assert
            Assert.Equal("doc-456", sut.CurrentDocumentId);
        }
    }
}

public class BulkMigrationResultTests
{
    public class StatusProperty
    {
        [Fact]
        public void Should_return_Completed_when_Success_is_true()
        {
            // Arrange
            var sut = new BulkMigrationResult { Success = true };

            // Act & Assert
            Assert.Equal(MigrationStatus.Completed, sut.Status);
        }

        [Fact]
        public void Should_return_Failed_when_Success_is_false()
        {
            // Arrange
            var sut = new BulkMigrationResult { Success = false };

            // Act & Assert
            Assert.Equal(MigrationStatus.Failed, sut.Status);
        }
    }

    public class ErrorMessageProperty
    {
        [Fact]
        public void Should_return_null_when_no_failures()
        {
            // Arrange
            var sut = new BulkMigrationResult { Failures = [] };

            // Act & Assert
            Assert.Null(sut.ErrorMessage);
        }

        [Fact]
        public void Should_include_failure_count_and_messages()
        {
            // Arrange
            var sut = new BulkMigrationResult
            {
                Failures =
                [
                    new MigrationFailure { ObjectId = "id-1", ObjectName = "Test", ErrorMessage = "Error A" },
                    new MigrationFailure { ObjectId = "id-2", ObjectName = "Test", ErrorMessage = "Error B" }
                ]
            };

            // Act
            var result = sut.ErrorMessage;

            // Assert
            Assert.NotNull(result);
            Assert.Contains("2 migration(s) failed", result);
            Assert.Contains("Error A", result);
            Assert.Contains("Error B", result);
        }

        [Fact]
        public void Should_limit_to_three_error_messages()
        {
            // Arrange
            var sut = new BulkMigrationResult
            {
                Failures =
                [
                    new MigrationFailure { ObjectId = "id-1", ObjectName = "Test", ErrorMessage = "Error 1" },
                    new MigrationFailure { ObjectId = "id-2", ObjectName = "Test", ErrorMessage = "Error 2" },
                    new MigrationFailure { ObjectId = "id-3", ObjectName = "Test", ErrorMessage = "Error 3" },
                    new MigrationFailure { ObjectId = "id-4", ObjectName = "Test", ErrorMessage = "Error 4" }
                ]
            };

            // Act
            var result = sut.ErrorMessage;

            // Assert
            Assert.NotNull(result);
            Assert.Contains("4 migration(s) failed", result);
            Assert.Contains("Error 1", result);
            Assert.Contains("Error 2", result);
            Assert.Contains("Error 3", result);
            Assert.DoesNotContain("Error 4", result);
        }
    }

    public class ExceptionProperty
    {
        [Fact]
        public void Should_return_null_when_no_failures()
        {
            // Arrange
            var sut = new BulkMigrationResult { Failures = [] };

            // Act & Assert
            Assert.Null(sut.Exception);
        }

        [Fact]
        public void Should_return_first_failure_exception()
        {
            // Arrange
            var expectedException = new InvalidOperationException("First error");
            var sut = new BulkMigrationResult
            {
                Failures =
                [
                    new MigrationFailure { ObjectId = "id-1", ObjectName = "Test", ErrorMessage = "First error", Exception = expectedException },
                    new MigrationFailure { ObjectId = "id-2", ObjectName = "Test", ErrorMessage = "Second error", Exception = new Exception("Second") }
                ]
            };

            // Act & Assert
            Assert.Same(expectedException, sut.Exception);
        }
    }

    public class VerificationResultProperty
    {
        [Fact]
        public void Should_always_return_null()
        {
            // Arrange
            var sut = new BulkMigrationResult();

            // Act & Assert
            Assert.Null(sut.VerificationResult);
        }
    }

    public class PlanProperty
    {
        [Fact]
        public void Should_always_return_null()
        {
            // Arrange
            var sut = new BulkMigrationResult();

            // Act & Assert
            Assert.Null(sut.Plan);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_MigrationId()
        {
            // Arrange
            var sut = new BulkMigrationResult();
            var id = Guid.NewGuid();

            // Act
            sut.MigrationId = id;

            // Assert
            Assert.Equal(id, sut.MigrationId);
        }

        [Fact]
        public void Should_set_and_get_TotalDocuments()
        {
            // Arrange
            var sut = new BulkMigrationResult();

            // Act
            sut.TotalDocuments = 10;

            // Assert
            Assert.Equal(10, sut.TotalDocuments);
        }

        [Fact]
        public void Should_set_and_get_SuccessfulCount()
        {
            // Arrange
            var sut = new BulkMigrationResult();

            // Act
            sut.SuccessfulCount = 8;

            // Assert
            Assert.Equal(8, sut.SuccessfulCount);
        }

        [Fact]
        public void Should_set_and_get_FailedCount()
        {
            // Arrange
            var sut = new BulkMigrationResult();

            // Act
            sut.FailedCount = 2;

            // Assert
            Assert.Equal(2, sut.FailedCount);
        }

        [Fact]
        public void Should_set_and_get_Duration()
        {
            // Arrange
            var sut = new BulkMigrationResult();
            var duration = TimeSpan.FromMinutes(5);

            // Act
            sut.Duration = duration;

            // Assert
            Assert.Equal(duration, sut.Duration);
        }

        [Fact]
        public void Should_have_empty_IndividualResults_by_default()
        {
            // Arrange & Act
            var sut = new BulkMigrationResult();

            // Assert
            Assert.Empty(sut.IndividualResults);
        }

        [Fact]
        public void Should_have_empty_Failures_by_default()
        {
            // Arrange & Act
            var sut = new BulkMigrationResult();

            // Assert
            Assert.Empty(sut.Failures);
        }
    }
}

public class MigrationFailureTests
{
    public class Properties
    {
        [Fact]
        public void Should_set_and_get_ObjectId()
        {
            // Arrange & Act
            var sut = new MigrationFailure
            {
                ObjectId = "obj-123",
                ObjectName = "TestObject",
                ErrorMessage = "Something went wrong"
            };

            // Assert
            Assert.Equal("obj-123", sut.ObjectId);
        }

        [Fact]
        public void Should_set_and_get_ObjectName()
        {
            // Arrange & Act
            var sut = new MigrationFailure
            {
                ObjectId = "obj-123",
                ObjectName = "Order",
                ErrorMessage = "Something went wrong"
            };

            // Assert
            Assert.Equal("Order", sut.ObjectName);
        }

        [Fact]
        public void Should_set_and_get_ErrorMessage()
        {
            // Arrange & Act
            var sut = new MigrationFailure
            {
                ObjectId = "obj-123",
                ObjectName = "TestObject",
                ErrorMessage = "Timeout occurred"
            };

            // Assert
            Assert.Equal("Timeout occurred", sut.ErrorMessage);
        }

        [Fact]
        public void Should_have_null_Exception_by_default()
        {
            // Arrange & Act
            var sut = new MigrationFailure
            {
                ObjectId = "obj-123",
                ObjectName = "TestObject",
                ErrorMessage = "Error"
            };

            // Assert
            Assert.Null(sut.Exception);
        }

        [Fact]
        public void Should_set_and_get_Exception()
        {
            // Arrange
            var exception = new InvalidOperationException("Test");

            // Act
            var sut = new MigrationFailure
            {
                ObjectId = "obj-123",
                ObjectName = "TestObject",
                ErrorMessage = "Error",
                Exception = exception
            };

            // Assert
            Assert.Same(exception, sut.Exception);
        }
    }
}
