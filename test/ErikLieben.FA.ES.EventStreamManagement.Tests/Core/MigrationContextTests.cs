using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using NSubstitute;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Core;

public class MigrationContextTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_generate_new_migration_id_by_default()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target"
            };

            // Assert
            Assert.NotEqual(Guid.Empty, sut.MigrationId);
        }

        [Fact]
        public void Should_have_copy_and_transform_strategy_by_default()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target"
            };

            // Assert
            Assert.Equal(MigrationStrategy.CopyAndTransform, sut.Strategy);
        }

        [Fact]
        public void Should_have_null_optional_properties_by_default()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target"
            };

            // Assert
            Assert.Null(sut.Transformer);
            Assert.Null(sut.Pipeline);
            Assert.Null(sut.LockOptions);
            Assert.Null(sut.BackupConfig);
            Assert.Null(sut.BookClosingConfig);
            Assert.Null(sut.VerificationConfig);
            Assert.Null(sut.ProgressConfig);
            Assert.Null(sut.DataStore);
            Assert.Null(sut.DocumentStore);
        }

        [Fact]
        public void Should_have_false_for_boolean_properties_by_default()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target"
            };

            // Assert
            Assert.False(sut.IsDryRun);
            Assert.False(sut.SupportsPause);
            Assert.False(sut.SupportsRollback);
        }

        [Fact]
        public void Should_have_empty_metadata_dictionary_by_default()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target"
            };

            // Assert
            Assert.NotNull(sut.Metadata);
            Assert.Empty(sut.Metadata);
        }

        [Fact]
        public void Should_set_started_at_to_current_time()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();
            var before = DateTimeOffset.UtcNow;

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target"
            };
            var after = DateTimeOffset.UtcNow;

            // Assert
            Assert.True(sut.StartedAt >= before);
            Assert.True(sut.StartedAt <= after);
        }
    }

    public class RequiredProperties
    {
        [Fact]
        public void Should_require_source_document()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target"
            };

            // Assert
            Assert.Same(document, sut.SourceDocument);
        }

        [Fact]
        public void Should_require_source_stream_identifier()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "my-source-stream",
                TargetStreamIdentifier = "target"
            };

            // Assert
            Assert.Equal("my-source-stream", sut.SourceStreamIdentifier);
        }

        [Fact]
        public void Should_require_target_stream_identifier()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();

            // Act
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "my-target-stream"
            };

            // Assert
            Assert.Equal("my-target-stream", sut.TargetStreamIdentifier);
        }
    }

    public class MetadataTests
    {
        [Fact]
        public void Should_allow_adding_metadata()
        {
            // Arrange
            var document = Substitute.For<IObjectDocument>();
            var sut = new MigrationContext
            {
                SourceDocument = document,
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target"
            };

            // Act
            sut.Metadata["key1"] = "value1";
            sut.Metadata["key2"] = 42;

            // Assert
            Assert.Equal(2, sut.Metadata.Count);
            Assert.Equal("value1", sut.Metadata["key1"]);
            Assert.Equal(42, sut.Metadata["key2"]);
        }
    }
}

public class BackupConfigurationTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_azure_blob_as_default_provider()
        {
            // Arrange & Act
            var sut = new BackupConfiguration();

            // Assert
            Assert.Equal("azure-blob", sut.ProviderName);
        }

        [Fact]
        public void Should_have_null_location_by_default()
        {
            // Arrange & Act
            var sut = new BackupConfiguration();

            // Assert
            Assert.Null(sut.Location);
        }

        [Fact]
        public void Should_have_false_for_include_options_by_default()
        {
            // Arrange & Act
            var sut = new BackupConfiguration();

            // Assert
            Assert.False(sut.IncludeSnapshots);
            Assert.False(sut.IncludeObjectDocument);
            Assert.False(sut.IncludeTerminatedStreams);
            Assert.False(sut.EnableCompression);
        }

        [Fact]
        public void Should_have_null_retention_by_default()
        {
            // Arrange & Act
            var sut = new BackupConfiguration();

            // Assert
            Assert.Null(sut.Retention);
        }
    }

    public class PropertySetters
    {
        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange & Act
            var sut = new BackupConfiguration
            {
                ProviderName = "custom-provider",
                Location = "/backups/migration",
                IncludeSnapshots = true,
                IncludeObjectDocument = true,
                IncludeTerminatedStreams = true,
                EnableCompression = true,
                Retention = TimeSpan.FromDays(30)
            };

            // Assert
            Assert.Equal("custom-provider", sut.ProviderName);
            Assert.Equal("/backups/migration", sut.Location);
            Assert.True(sut.IncludeSnapshots);
            Assert.True(sut.IncludeObjectDocument);
            Assert.True(sut.IncludeTerminatedStreams);
            Assert.True(sut.EnableCompression);
            Assert.Equal(TimeSpan.FromDays(30), sut.Retention);
        }
    }
}

public class BookClosingConfigurationTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_null_reason_by_default()
        {
            // Arrange & Act
            var sut = new BookClosingConfiguration();

            // Assert
            Assert.Null(sut.Reason);
        }

        [Fact]
        public void Should_have_false_for_create_snapshot_by_default()
        {
            // Arrange & Act
            var sut = new BookClosingConfiguration();

            // Assert
            Assert.False(sut.CreateSnapshot);
        }

        [Fact]
        public void Should_have_null_archive_location_by_default()
        {
            // Arrange & Act
            var sut = new BookClosingConfiguration();

            // Assert
            Assert.Null(sut.ArchiveLocation);
        }

        [Fact]
        public void Should_have_false_for_mark_as_deleted_by_default()
        {
            // Arrange & Act
            var sut = new BookClosingConfiguration();

            // Assert
            Assert.False(sut.MarkAsDeleted);
        }

        [Fact]
        public void Should_have_empty_metadata_by_default()
        {
            // Arrange & Act
            var sut = new BookClosingConfiguration();

            // Assert
            Assert.NotNull(sut.Metadata);
            Assert.Empty(sut.Metadata);
        }
    }

    public class PropertySetters
    {
        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange & Act
            var sut = new BookClosingConfiguration
            {
                Reason = "Migration completed",
                CreateSnapshot = true,
                ArchiveLocation = "/archives/2024",
                MarkAsDeleted = true
            };
            sut.Metadata["key"] = "value";

            // Assert
            Assert.Equal("Migration completed", sut.Reason);
            Assert.True(sut.CreateSnapshot);
            Assert.Equal("/archives/2024", sut.ArchiveLocation);
            Assert.True(sut.MarkAsDeleted);
            Assert.Single(sut.Metadata);
        }
    }
}

public class VerificationConfigurationTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_false_for_all_verification_options_by_default()
        {
            // Arrange & Act
            var sut = new VerificationConfiguration();

            // Assert
            Assert.False(sut.CompareEventCounts);
            Assert.False(sut.CompareChecksums);
            Assert.False(sut.ValidateTransformations);
            Assert.False(sut.VerifyStreamIntegrity);
            Assert.False(sut.FailFast);
        }

        [Fact]
        public void Should_have_100_as_default_sample_size()
        {
            // Arrange & Act
            var sut = new VerificationConfiguration();

            // Assert
            Assert.Equal(100, sut.TransformationSampleSize);
        }

        [Fact]
        public void Should_have_empty_custom_validations_by_default()
        {
            // Arrange & Act
            var sut = new VerificationConfiguration();

            // Assert
            Assert.NotNull(sut.CustomValidations);
            Assert.Empty(sut.CustomValidations);
        }
    }

    public class PropertySetters
    {
        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange & Act
            var sut = new VerificationConfiguration
            {
                CompareEventCounts = true,
                CompareChecksums = true,
                ValidateTransformations = true,
                TransformationSampleSize = 50,
                VerifyStreamIntegrity = true,
                FailFast = true
            };

            // Assert
            Assert.True(sut.CompareEventCounts);
            Assert.True(sut.CompareChecksums);
            Assert.True(sut.ValidateTransformations);
            Assert.Equal(50, sut.TransformationSampleSize);
            Assert.True(sut.VerifyStreamIntegrity);
            Assert.True(sut.FailFast);
        }
    }
}

public class ProgressConfigurationTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_5_seconds_as_default_report_interval()
        {
            // Arrange & Act
            var sut = new ProgressConfiguration();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(5), sut.ReportInterval);
        }

        [Fact]
        public void Should_have_null_callbacks_by_default()
        {
            // Arrange & Act
            var sut = new ProgressConfiguration();

            // Assert
            Assert.Null(sut.OnProgress);
            Assert.Null(sut.OnCompleted);
            Assert.Null(sut.OnFailed);
        }

        [Fact]
        public void Should_have_false_for_enable_logging_by_default()
        {
            // Arrange & Act
            var sut = new ProgressConfiguration();

            // Assert
            Assert.False(sut.EnableLogging);
        }

        [Fact]
        public void Should_have_empty_custom_metrics_by_default()
        {
            // Arrange & Act
            var sut = new ProgressConfiguration();

            // Assert
            Assert.NotNull(sut.CustomMetrics);
            Assert.Empty(sut.CustomMetrics);
        }
    }

    public class PropertySetters
    {
        [Fact]
        public void Should_allow_setting_callbacks()
        {
            // Arrange
            var progressCalled = false;
            var completedCalled = false;
            var failedCalled = false;

            // Act
            var sut = new ProgressConfiguration
            {
                OnProgress = _ => progressCalled = true,
                OnCompleted = _ => completedCalled = true,
                OnFailed = (_, _) => failedCalled = true,
                EnableLogging = true,
                ReportInterval = TimeSpan.FromSeconds(10)
            };

            // Invoke callbacks
            sut.OnProgress?.Invoke(null!);
            sut.OnCompleted?.Invoke(null!);
            sut.OnFailed?.Invoke(null!, null!);

            // Assert
            Assert.True(progressCalled);
            Assert.True(completedCalled);
            Assert.True(failedCalled);
            Assert.True(sut.EnableLogging);
            Assert.Equal(TimeSpan.FromSeconds(10), sut.ReportInterval);
        }
    }
}
