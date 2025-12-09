using ErikLieben.FA.ES.EventStreamManagement.Backup;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Backup;

public class BackupBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange & Act
            var sut = new BackupBuilder();

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ToProviderMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.ToProvider("azure-blob");

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData("azure-blob")]
        [InlineData("aws-s3")]
        [InlineData("local-fs")]
        [InlineData("custom")]
        public void Should_accept_various_provider_names(string providerName)
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.ToProvider(providerName);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class ToLocationMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.ToLocation("/backups/2024");

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData("/backups")]
        [InlineData("https://storage.blob.core.windows.net/backups")]
        [InlineData("s3://bucket/backups")]
        public void Should_accept_various_locations(string location)
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.ToLocation(location);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class IncludeSnapshotsMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.IncludeSnapshots();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class IncludeObjectDocumentMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.IncludeObjectDocument();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class IncludeTerminatedStreamsMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.IncludeTerminatedStreams();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithCompressionMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.WithCompression();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class WithRetentionMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.WithRetention(TimeSpan.FromDays(30));

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(30)]
        [InlineData(365)]
        public void Should_accept_various_retention_periods(int days)
        {
            // Arrange
            var sut = new BackupBuilder();

            // Act
            var result = sut.WithRetention(TimeSpan.FromDays(days));

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class FluentChainingTests
    {
        [Fact]
        public void Should_support_full_fluent_configuration()
        {
            // Arrange & Act
            var sut = new BackupBuilder()
                .ToProvider("azure-blob")
                .ToLocation("/backups/migration")
                .IncludeSnapshots()
                .IncludeObjectDocument()
                .IncludeTerminatedStreams()
                .WithCompression()
                .WithRetention(TimeSpan.FromDays(90));

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class InterfaceImplementation
    {
        [Fact]
        public void Should_implement_IBackupBuilder()
        {
            // Arrange & Act
            var sut = new BackupBuilder();

            // Assert
            Assert.IsAssignableFrom<IBackupBuilder>(sut);
        }
    }
}
