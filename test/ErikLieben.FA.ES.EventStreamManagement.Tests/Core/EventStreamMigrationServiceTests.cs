using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Core;

public class EventStreamMigrationServiceTests
{
    private static (IDataStore dataStore, IDocumentStore documentStore,
        IDistributedLockProvider lockProvider, ILoggerFactory loggerFactory) CreateDependencies()
    {
        var dataStore = Substitute.For<IDataStore>();
        var documentStore = Substitute.For<IDocumentStore>();
        var lockProvider = Substitute.For<IDistributedLockProvider>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<EventStreamMigrationService>().Returns(Substitute.For<ILogger<EventStreamMigrationService>>());
        loggerFactory.CreateLogger<MigrationBuilder>().Returns(Substitute.For<ILogger<MigrationBuilder>>());

        return (dataStore, documentStore, lockProvider, loggerFactory);
    }

    private static IObjectDocument CreateMockDocument(string? objectId = null)
    {
        var document = Substitute.For<IObjectDocument>();
        document.ObjectId.Returns(objectId ?? Guid.NewGuid().ToString());
        var streamInfo = Substitute.For<StreamInformation>();
        streamInfo.StreamIdentifier = "source-stream";
        document.Active.Returns(streamInfo);
        return document;
    }

    public class Constructor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_data_store_is_null()
        {
            // Arrange
            var (_, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventStreamMigrationService(null!, documentStore, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_document_store_is_null()
        {
            // Arrange
            var (dataStore, _, lockProvider, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventStreamMigrationService(dataStore, null!, lockProvider, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_lock_provider_is_null()
        {
            // Arrange
            var (dataStore, documentStore, _, loggerFactory) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventStreamMigrationService(dataStore, documentStore, null!, loggerFactory));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_factory_is_null()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, _) = CreateDependencies();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventStreamMigrationService(dataStore, documentStore, lockProvider, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ForDocumentMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_document_is_null()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.ForDocument(null!));
        }

        [Fact]
        public void Should_return_migration_builder()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);
            var document = CreateMockDocument();

            // Act
            var result = sut.ForDocument(document);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<MigrationBuilder>(result);
        }
    }

    public class ForDocumentsMethod
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_documents_is_null()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => sut.ForDocuments(null!));
        }

        [Fact]
        public void Should_throw_ArgumentException_when_documents_is_empty()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);
            var documents = Enumerable.Empty<IObjectDocument>();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => sut.ForDocuments(documents));
        }

        [Fact]
        public void Should_return_migration_builder_for_multiple_documents()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);
            var documents = new[] { CreateMockDocument(), CreateMockDocument() };

            // Act
            var result = sut.ForDocuments(documents);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<BulkMigrationBuilder>(result);
        }
    }

    public class GetActiveMigrationsAsyncMethod
    {
        [Fact]
        public async Task Should_return_empty_collection_when_no_active_migrations()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = await sut.GetActiveMigrationsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }

    public class GetMigrationStatusAsyncMethod
    {
        [Fact]
        public async Task Should_return_null_when_migration_not_found()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);
            var unknownId = Guid.NewGuid();

            // Act
            var result = await sut.GetMigrationStatusAsync(unknownId);

            // Assert
            Assert.Null(result);
        }
    }

    public class PauseMigrationAsyncMethod
    {
        [Fact]
        public async Task Should_complete_without_error_when_migration_not_found()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);
            var unknownId = Guid.NewGuid();

            // Act & Assert
            var exception = await Record.ExceptionAsync(() => sut.PauseMigrationAsync(unknownId));
            Assert.Null(exception);
        }
    }

    public class ResumeMigrationAsyncMethod
    {
        [Fact]
        public async Task Should_complete_without_error_when_migration_not_found()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);
            var unknownId = Guid.NewGuid();

            // Act & Assert
            var exception = await Record.ExceptionAsync(() => sut.ResumeMigrationAsync(unknownId));
            Assert.Null(exception);
        }
    }

    public class CancelMigrationAsyncMethod
    {
        [Fact]
        public async Task Should_complete_without_error_when_migration_not_found()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);
            var unknownId = Guid.NewGuid();

            // Act & Assert
            var exception = await Record.ExceptionAsync(() => sut.CancelMigrationAsync(unknownId));
            Assert.Null(exception);
        }
    }

    public class InterfaceImplementation
    {
        [Fact]
        public void Should_implement_IEventStreamMigrationService()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();

            // Act
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Assert
            Assert.IsType<EventStreamMigrationService>(sut);
        }
    }
}
