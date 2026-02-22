using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Core;

public class EventStreamMigrationServiceExtendedTests
{
    private static (IDataStore dataStore, IDocumentStore documentStore, IDistributedLockProvider lockProvider, ILoggerFactory loggerFactory) CreateDependencies()
    {
        var dataStore = Substitute.For<IDataStore>();
        var documentStore = Substitute.For<IDocumentStore>();
        var lockProvider = Substitute.For<IDistributedLockProvider>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<EventStreamMigrationService>().Returns(Substitute.For<ILogger<EventStreamMigrationService>>());
        loggerFactory.CreateLogger<MigrationBuilder>().Returns(Substitute.For<ILogger<MigrationBuilder>>());
        loggerFactory.CreateLogger<MigrationExecutor>().Returns(Substitute.For<ILogger<MigrationExecutor>>());
        loggerFactory.CreateLogger<MigrationProgressTracker>().Returns(Substitute.For<ILogger<MigrationProgressTracker>>());

        return (dataStore, documentStore, lockProvider, loggerFactory);
    }

    private static IObjectDocument CreateMockDocument(string objectId = "test-object")
    {
        var document = Substitute.For<IObjectDocument>();
        var streamInfo = Substitute.For<StreamInformation>();
        streamInfo.StreamIdentifier = "test-stream";
        document.Active.Returns(streamInfo);
        document.ObjectId.Returns(objectId);
        return document;
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

            // Act & Assert
            Assert.Throws<ArgumentException>(() => sut.ForDocuments(Array.Empty<IObjectDocument>()));
        }

        [Fact]
        public void Should_return_builder_for_multiple_documents()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            var doc1 = CreateMockDocument("object-1");
            var doc2 = CreateMockDocument("object-2");

            // Act
            var result = sut.ForDocuments(new[] { doc1, doc2 });

            // Assert
            Assert.NotNull(result);
        }
    }

    public class GetActiveMigrationsAsyncMethod
    {
        [Fact]
        public async Task Should_return_empty_when_no_active_migrations()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            var result = await sut.GetActiveMigrationsAsync();

            // Assert
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

            // Act
            var result = await sut.GetMigrationStatusAsync(Guid.NewGuid());

            // Assert
            Assert.Null(result);
        }
    }

    public class PauseMigrationAsyncMethod
    {
        [Fact]
        public async Task Should_complete_when_migration_not_found()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            await sut.PauseMigrationAsync(Guid.NewGuid());

            // Assert - completes without throwing for non-existent migration
            Assert.True(true);
        }
    }

    public class ResumeMigrationAsyncMethod
    {
        [Fact]
        public async Task Should_complete_when_migration_not_found()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            await sut.ResumeMigrationAsync(Guid.NewGuid());

            // Assert - completes without throwing for non-existent migration
            Assert.True(true);
        }
    }

    public class CancelMigrationAsyncMethod
    {
        [Fact]
        public async Task Should_complete_when_migration_not_found()
        {
            // Arrange
            var (dataStore, documentStore, lockProvider, loggerFactory) = CreateDependencies();
            var sut = new EventStreamMigrationService(dataStore, documentStore, lockProvider, loggerFactory);

            // Act
            await sut.CancelMigrationAsync(Guid.NewGuid());

            // Assert - completes without throwing for non-existent migration
            Assert.True(true);
        }
    }
}
