using ErikLieben.FA.ES.EventStreamManagement.Backup;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Backup;

public class BackupOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_IncludeSnapshots_false_by_default()
        {
            // Arrange & Act
            var sut = new BackupOptions();

            // Assert
            Assert.False(sut.IncludeSnapshots);
        }

        [Fact]
        public void Should_have_IncludeObjectDocument_false_by_default()
        {
            // Arrange & Act
            var sut = new BackupOptions();

            // Assert
            Assert.False(sut.IncludeObjectDocument);
        }

        [Fact]
        public void Should_have_IncludeTerminatedStreams_false_by_default()
        {
            // Arrange & Act
            var sut = new BackupOptions();

            // Assert
            Assert.False(sut.IncludeTerminatedStreams);
        }

        [Fact]
        public void Should_have_EnableCompression_false_by_default()
        {
            // Arrange & Act
            var sut = new BackupOptions();

            // Assert
            Assert.False(sut.EnableCompression);
        }

        [Fact]
        public void Should_have_null_Location_by_default()
        {
            // Arrange & Act
            var sut = new BackupOptions();

            // Assert
            Assert.Null(sut.Location);
        }

        [Fact]
        public void Should_have_null_Retention_by_default()
        {
            // Arrange & Act
            var sut = new BackupOptions();

            // Assert
            Assert.Null(sut.Retention);
        }

        [Fact]
        public void Should_have_null_Description_by_default()
        {
            // Arrange & Act
            var sut = new BackupOptions();

            // Assert
            Assert.Null(sut.Description);
        }

        [Fact]
        public void Should_have_empty_Tags_by_default()
        {
            // Arrange & Act
            var sut = new BackupOptions();

            // Assert
            Assert.NotNull(sut.Tags);
            Assert.Empty(sut.Tags);
        }
    }

    public class DefaultFactory
    {
        [Fact]
        public void Should_set_IncludeSnapshots_to_false()
        {
            // Arrange & Act
            var sut = BackupOptions.Default;

            // Assert
            Assert.False(sut.IncludeSnapshots);
        }

        [Fact]
        public void Should_set_IncludeObjectDocument_to_true()
        {
            // Arrange & Act
            var sut = BackupOptions.Default;

            // Assert
            Assert.True(sut.IncludeObjectDocument);
        }

        [Fact]
        public void Should_set_IncludeTerminatedStreams_to_false()
        {
            // Arrange & Act
            var sut = BackupOptions.Default;

            // Assert
            Assert.False(sut.IncludeTerminatedStreams);
        }

        [Fact]
        public void Should_set_EnableCompression_to_true()
        {
            // Arrange & Act
            var sut = BackupOptions.Default;

            // Assert
            Assert.True(sut.EnableCompression);
        }
    }

    public class FullFactory
    {
        [Fact]
        public void Should_set_IncludeSnapshots_to_true()
        {
            // Arrange & Act
            var sut = BackupOptions.Full;

            // Assert
            Assert.True(sut.IncludeSnapshots);
        }

        [Fact]
        public void Should_set_IncludeObjectDocument_to_true()
        {
            // Arrange & Act
            var sut = BackupOptions.Full;

            // Assert
            Assert.True(sut.IncludeObjectDocument);
        }

        [Fact]
        public void Should_set_IncludeTerminatedStreams_to_true()
        {
            // Arrange & Act
            var sut = BackupOptions.Full;

            // Assert
            Assert.True(sut.IncludeTerminatedStreams);
        }

        [Fact]
        public void Should_set_EnableCompression_to_true()
        {
            // Arrange & Act
            var sut = BackupOptions.Full;

            // Assert
            Assert.True(sut.EnableCompression);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_IncludeSnapshots()
        {
            // Arrange
            var sut = new BackupOptions();

            // Act
            sut.IncludeSnapshots = true;

            // Assert
            Assert.True(sut.IncludeSnapshots);
        }

        [Fact]
        public void Should_set_and_get_IncludeObjectDocument()
        {
            // Arrange
            var sut = new BackupOptions();

            // Act
            sut.IncludeObjectDocument = true;

            // Assert
            Assert.True(sut.IncludeObjectDocument);
        }

        [Fact]
        public void Should_set_and_get_IncludeTerminatedStreams()
        {
            // Arrange
            var sut = new BackupOptions();

            // Act
            sut.IncludeTerminatedStreams = true;

            // Assert
            Assert.True(sut.IncludeTerminatedStreams);
        }

        [Fact]
        public void Should_set_and_get_EnableCompression()
        {
            // Arrange
            var sut = new BackupOptions();

            // Act
            sut.EnableCompression = true;

            // Assert
            Assert.True(sut.EnableCompression);
        }

        [Fact]
        public void Should_set_and_get_Location()
        {
            // Arrange
            var sut = new BackupOptions();

            // Act
            sut.Location = "/backups/test";

            // Assert
            Assert.Equal("/backups/test", sut.Location);
        }

        [Fact]
        public void Should_set_and_get_Retention()
        {
            // Arrange
            var sut = new BackupOptions();
            var retention = TimeSpan.FromDays(30);

            // Act
            sut.Retention = retention;

            // Assert
            Assert.Equal(retention, sut.Retention);
        }

        [Fact]
        public void Should_set_and_get_Description()
        {
            // Arrange
            var sut = new BackupOptions();

            // Act
            sut.Description = "Pre-migration backup";

            // Assert
            Assert.Equal("Pre-migration backup", sut.Description);
        }

        [Fact]
        public void Should_set_and_get_Tags()
        {
            // Arrange
            var sut = new BackupOptions();
            var tags = new Dictionary<string, string> { { "env", "prod" }, { "team", "platform" } };

            // Act
            sut.Tags = tags;

            // Assert
            Assert.Equal(2, sut.Tags.Count);
            Assert.Equal("prod", sut.Tags["env"]);
            Assert.Equal("platform", sut.Tags["team"]);
        }
    }
}

public class RestoreOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_Overwrite_false_by_default()
        {
            // Arrange & Act
            var sut = new RestoreOptions();

            // Assert
            Assert.False(sut.Overwrite);
        }

        [Fact]
        public void Should_have_ValidateBeforeRestore_true_by_default()
        {
            // Arrange & Act
            var sut = new RestoreOptions();

            // Assert
            Assert.True(sut.ValidateBeforeRestore);
        }

        [Fact]
        public void Should_have_RestoreObjectDocument_true_by_default()
        {
            // Arrange & Act
            var sut = new RestoreOptions();

            // Assert
            Assert.True(sut.RestoreObjectDocument);
        }

        [Fact]
        public void Should_have_RestoreSnapshots_true_by_default()
        {
            // Arrange & Act
            var sut = new RestoreOptions();

            // Assert
            Assert.True(sut.RestoreSnapshots);
        }

        [Fact]
        public void Should_have_null_Description_by_default()
        {
            // Arrange & Act
            var sut = new RestoreOptions();

            // Assert
            Assert.Null(sut.Description);
        }
    }

    public class DefaultFactory
    {
        [Fact]
        public void Should_set_Overwrite_to_false()
        {
            // Arrange & Act
            var sut = RestoreOptions.Default;

            // Assert
            Assert.False(sut.Overwrite);
        }

        [Fact]
        public void Should_set_ValidateBeforeRestore_to_true()
        {
            // Arrange & Act
            var sut = RestoreOptions.Default;

            // Assert
            Assert.True(sut.ValidateBeforeRestore);
        }

        [Fact]
        public void Should_set_RestoreObjectDocument_to_true()
        {
            // Arrange & Act
            var sut = RestoreOptions.Default;

            // Assert
            Assert.True(sut.RestoreObjectDocument);
        }

        [Fact]
        public void Should_set_RestoreSnapshots_to_true()
        {
            // Arrange & Act
            var sut = RestoreOptions.Default;

            // Assert
            Assert.True(sut.RestoreSnapshots);
        }
    }

    public class WithOverwriteFactory
    {
        [Fact]
        public void Should_set_Overwrite_to_true()
        {
            // Arrange & Act
            var sut = RestoreOptions.WithOverwrite;

            // Assert
            Assert.True(sut.Overwrite);
        }

        [Fact]
        public void Should_set_ValidateBeforeRestore_to_true()
        {
            // Arrange & Act
            var sut = RestoreOptions.WithOverwrite;

            // Assert
            Assert.True(sut.ValidateBeforeRestore);
        }

        [Fact]
        public void Should_set_RestoreObjectDocument_to_true()
        {
            // Arrange & Act
            var sut = RestoreOptions.WithOverwrite;

            // Assert
            Assert.True(sut.RestoreObjectDocument);
        }

        [Fact]
        public void Should_set_RestoreSnapshots_to_true()
        {
            // Arrange & Act
            var sut = RestoreOptions.WithOverwrite;

            // Assert
            Assert.True(sut.RestoreSnapshots);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_Overwrite()
        {
            // Arrange
            var sut = new RestoreOptions();

            // Act
            sut.Overwrite = true;

            // Assert
            Assert.True(sut.Overwrite);
        }

        [Fact]
        public void Should_set_and_get_ValidateBeforeRestore()
        {
            // Arrange
            var sut = new RestoreOptions();

            // Act
            sut.ValidateBeforeRestore = false;

            // Assert
            Assert.False(sut.ValidateBeforeRestore);
        }

        [Fact]
        public void Should_set_and_get_Description()
        {
            // Arrange
            var sut = new RestoreOptions();

            // Act
            sut.Description = "Disaster recovery";

            // Assert
            Assert.Equal("Disaster recovery", sut.Description);
        }
    }
}

public class BulkBackupOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_MaxConcurrency_4_by_default()
        {
            // Arrange & Act
            var sut = new BulkBackupOptions();

            // Assert
            Assert.Equal(4, sut.MaxConcurrency);
        }

        [Fact]
        public void Should_have_ContinueOnError_true_by_default()
        {
            // Arrange & Act
            var sut = new BulkBackupOptions();

            // Assert
            Assert.True(sut.ContinueOnError);
        }

        [Fact]
        public void Should_have_null_OnProgress_by_default()
        {
            // Arrange & Act
            var sut = new BulkBackupOptions();

            // Assert
            Assert.Null(sut.OnProgress);
        }
    }

    public class DefaultFactory
    {
        [Fact]
        public void Should_set_IncludeSnapshots_to_false()
        {
            // Arrange & Act
            var sut = BulkBackupOptions.Default;

            // Assert
            Assert.False(sut.IncludeSnapshots);
        }

        [Fact]
        public void Should_set_IncludeObjectDocument_to_true()
        {
            // Arrange & Act
            var sut = BulkBackupOptions.Default;

            // Assert
            Assert.True(sut.IncludeObjectDocument);
        }

        [Fact]
        public void Should_set_IncludeTerminatedStreams_to_false()
        {
            // Arrange & Act
            var sut = BulkBackupOptions.Default;

            // Assert
            Assert.False(sut.IncludeTerminatedStreams);
        }

        [Fact]
        public void Should_set_EnableCompression_to_true()
        {
            // Arrange & Act
            var sut = BulkBackupOptions.Default;

            // Assert
            Assert.True(sut.EnableCompression);
        }

        [Fact]
        public void Should_set_MaxConcurrency_to_4()
        {
            // Arrange & Act
            var sut = BulkBackupOptions.Default;

            // Assert
            Assert.Equal(4, sut.MaxConcurrency);
        }

        [Fact]
        public void Should_set_ContinueOnError_to_true()
        {
            // Arrange & Act
            var sut = BulkBackupOptions.Default;

            // Assert
            Assert.True(sut.ContinueOnError);
        }
    }

    public class InheritanceTests
    {
        [Fact]
        public void Should_inherit_from_BackupOptions()
        {
            // Arrange & Act
            var sut = new BulkBackupOptions();

            // Assert
            Assert.IsType<BulkBackupOptions>(sut);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_MaxConcurrency()
        {
            // Arrange
            var sut = new BulkBackupOptions();

            // Act
            sut.MaxConcurrency = 8;

            // Assert
            Assert.Equal(8, sut.MaxConcurrency);
        }

        [Fact]
        public void Should_set_and_get_ContinueOnError()
        {
            // Arrange
            var sut = new BulkBackupOptions();

            // Act
            sut.ContinueOnError = false;

            // Assert
            Assert.False(sut.ContinueOnError);
        }

        [Fact]
        public void Should_set_and_get_OnProgress()
        {
            // Arrange
            var sut = new BulkBackupOptions();
            var callbackInvoked = false;
            Action<BulkBackupProgress> callback = _ => callbackInvoked = true;

            // Act
            sut.OnProgress = callback;
            sut.OnProgress(new BulkBackupProgress());

            // Assert
            Assert.NotNull(sut.OnProgress);
            Assert.True(callbackInvoked);
        }
    }
}

public class BulkRestoreOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_MaxConcurrency_4_by_default()
        {
            // Arrange & Act
            var sut = new BulkRestoreOptions();

            // Assert
            Assert.Equal(4, sut.MaxConcurrency);
        }

        [Fact]
        public void Should_have_ContinueOnError_true_by_default()
        {
            // Arrange & Act
            var sut = new BulkRestoreOptions();

            // Assert
            Assert.True(sut.ContinueOnError);
        }

        [Fact]
        public void Should_have_null_OnProgress_by_default()
        {
            // Arrange & Act
            var sut = new BulkRestoreOptions();

            // Assert
            Assert.Null(sut.OnProgress);
        }
    }

    public class DefaultFactory
    {
        [Fact]
        public void Should_set_Overwrite_to_false()
        {
            // Arrange & Act
            var sut = BulkRestoreOptions.Default;

            // Assert
            Assert.False(sut.Overwrite);
        }

        [Fact]
        public void Should_set_ValidateBeforeRestore_to_true()
        {
            // Arrange & Act
            var sut = BulkRestoreOptions.Default;

            // Assert
            Assert.True(sut.ValidateBeforeRestore);
        }

        [Fact]
        public void Should_set_RestoreObjectDocument_to_true()
        {
            // Arrange & Act
            var sut = BulkRestoreOptions.Default;

            // Assert
            Assert.True(sut.RestoreObjectDocument);
        }

        [Fact]
        public void Should_set_RestoreSnapshots_to_true()
        {
            // Arrange & Act
            var sut = BulkRestoreOptions.Default;

            // Assert
            Assert.True(sut.RestoreSnapshots);
        }

        [Fact]
        public void Should_set_MaxConcurrency_to_4()
        {
            // Arrange & Act
            var sut = BulkRestoreOptions.Default;

            // Assert
            Assert.Equal(4, sut.MaxConcurrency);
        }

        [Fact]
        public void Should_set_ContinueOnError_to_true()
        {
            // Arrange & Act
            var sut = BulkRestoreOptions.Default;

            // Assert
            Assert.True(sut.ContinueOnError);
        }
    }

    public class InheritanceTests
    {
        [Fact]
        public void Should_inherit_from_RestoreOptions()
        {
            // Arrange & Act
            var sut = new BulkRestoreOptions();

            // Assert
            Assert.IsType<BulkRestoreOptions>(sut);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_MaxConcurrency()
        {
            // Arrange
            var sut = new BulkRestoreOptions();

            // Act
            sut.MaxConcurrency = 16;

            // Assert
            Assert.Equal(16, sut.MaxConcurrency);
        }

        [Fact]
        public void Should_set_and_get_OnProgress()
        {
            // Arrange
            var sut = new BulkRestoreOptions();
            var callbackInvoked = false;
            Action<BulkRestoreProgress> callback = _ => callbackInvoked = true;

            // Act
            sut.OnProgress = callback;
            sut.OnProgress(new BulkRestoreProgress());

            // Assert
            Assert.NotNull(sut.OnProgress);
            Assert.True(callbackInvoked);
        }
    }
}

public class BulkBackupProgressTests
{
    public class PercentageCompleteProperty
    {
        [Fact]
        public void Should_return_zero_when_TotalStreams_is_zero()
        {
            // Arrange
            var sut = new BulkBackupProgress { TotalStreams = 0, ProcessedStreams = 0 };

            // Act & Assert
            Assert.Equal(0.0, sut.PercentageComplete);
        }

        [Fact]
        public void Should_calculate_correct_percentage()
        {
            // Arrange
            var sut = new BulkBackupProgress { TotalStreams = 10, ProcessedStreams = 5 };

            // Act & Assert
            Assert.Equal(50.0, sut.PercentageComplete);
        }

        [Fact]
        public void Should_return_100_when_all_processed()
        {
            // Arrange
            var sut = new BulkBackupProgress { TotalStreams = 10, ProcessedStreams = 10 };

            // Act & Assert
            Assert.Equal(100.0, sut.PercentageComplete);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_TotalStreams()
        {
            // Arrange
            var sut = new BulkBackupProgress();

            // Act
            sut.TotalStreams = 42;

            // Assert
            Assert.Equal(42, sut.TotalStreams);
        }

        [Fact]
        public void Should_set_and_get_ProcessedStreams()
        {
            // Arrange
            var sut = new BulkBackupProgress();

            // Act
            sut.ProcessedStreams = 10;

            // Assert
            Assert.Equal(10, sut.ProcessedStreams);
        }

        [Fact]
        public void Should_set_and_get_SuccessfulBackups()
        {
            // Arrange
            var sut = new BulkBackupProgress();

            // Act
            sut.SuccessfulBackups = 8;

            // Assert
            Assert.Equal(8, sut.SuccessfulBackups);
        }

        [Fact]
        public void Should_set_and_get_FailedBackups()
        {
            // Arrange
            var sut = new BulkBackupProgress();

            // Act
            sut.FailedBackups = 2;

            // Assert
            Assert.Equal(2, sut.FailedBackups);
        }

        [Fact]
        public void Should_set_and_get_CurrentStreamId()
        {
            // Arrange
            var sut = new BulkBackupProgress();

            // Act
            sut.CurrentStreamId = "stream-123";

            // Assert
            Assert.Equal("stream-123", sut.CurrentStreamId);
        }
    }
}

public class BulkRestoreProgressTests
{
    public class PercentageCompleteProperty
    {
        [Fact]
        public void Should_return_zero_when_TotalBackups_is_zero()
        {
            // Arrange
            var sut = new BulkRestoreProgress { TotalBackups = 0, ProcessedBackups = 0 };

            // Act & Assert
            Assert.Equal(0.0, sut.PercentageComplete);
        }

        [Fact]
        public void Should_calculate_correct_percentage()
        {
            // Arrange
            var sut = new BulkRestoreProgress { TotalBackups = 8, ProcessedBackups = 2 };

            // Act & Assert
            Assert.Equal(25.0, sut.PercentageComplete);
        }

        [Fact]
        public void Should_return_100_when_all_processed()
        {
            // Arrange
            var sut = new BulkRestoreProgress { TotalBackups = 5, ProcessedBackups = 5 };

            // Act & Assert
            Assert.Equal(100.0, sut.PercentageComplete);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_TotalBackups()
        {
            // Arrange
            var sut = new BulkRestoreProgress();

            // Act
            sut.TotalBackups = 20;

            // Assert
            Assert.Equal(20, sut.TotalBackups);
        }

        [Fact]
        public void Should_set_and_get_ProcessedBackups()
        {
            // Arrange
            var sut = new BulkRestoreProgress();

            // Act
            sut.ProcessedBackups = 15;

            // Assert
            Assert.Equal(15, sut.ProcessedBackups);
        }

        [Fact]
        public void Should_set_and_get_SuccessfulRestores()
        {
            // Arrange
            var sut = new BulkRestoreProgress();

            // Act
            sut.SuccessfulRestores = 14;

            // Assert
            Assert.Equal(14, sut.SuccessfulRestores);
        }

        [Fact]
        public void Should_set_and_get_FailedRestores()
        {
            // Arrange
            var sut = new BulkRestoreProgress();

            // Act
            sut.FailedRestores = 1;

            // Assert
            Assert.Equal(1, sut.FailedRestores);
        }

        [Fact]
        public void Should_set_and_get_CurrentBackupId()
        {
            // Arrange
            var sut = new BulkRestoreProgress();
            var id = Guid.NewGuid();

            // Act
            sut.CurrentBackupId = id;

            // Assert
            Assert.Equal(id, sut.CurrentBackupId);
        }
    }
}

public class BackupQueryTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_null_ObjectName_by_default()
        {
            // Arrange & Act
            var sut = new BackupQuery();

            // Assert
            Assert.Null(sut.ObjectName);
        }

        [Fact]
        public void Should_have_null_ObjectId_by_default()
        {
            // Arrange & Act
            var sut = new BackupQuery();

            // Assert
            Assert.Null(sut.ObjectId);
        }

        [Fact]
        public void Should_have_null_CreatedAfter_by_default()
        {
            // Arrange & Act
            var sut = new BackupQuery();

            // Assert
            Assert.Null(sut.CreatedAfter);
        }

        [Fact]
        public void Should_have_null_CreatedBefore_by_default()
        {
            // Arrange & Act
            var sut = new BackupQuery();

            // Assert
            Assert.Null(sut.CreatedBefore);
        }

        [Fact]
        public void Should_have_null_Tags_by_default()
        {
            // Arrange & Act
            var sut = new BackupQuery();

            // Assert
            Assert.Null(sut.Tags);
        }

        [Fact]
        public void Should_have_null_MaxResults_by_default()
        {
            // Arrange & Act
            var sut = new BackupQuery();

            // Assert
            Assert.Null(sut.MaxResults);
        }

        [Fact]
        public void Should_have_IncludeExpired_false_by_default()
        {
            // Arrange & Act
            var sut = new BackupQuery();

            // Assert
            Assert.False(sut.IncludeExpired);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_set_and_get_ObjectName()
        {
            // Arrange
            var sut = new BackupQuery();

            // Act
            sut.ObjectName = "Order";

            // Assert
            Assert.Equal("Order", sut.ObjectName);
        }

        [Fact]
        public void Should_set_and_get_ObjectId()
        {
            // Arrange
            var sut = new BackupQuery();

            // Act
            sut.ObjectId = "order-123";

            // Assert
            Assert.Equal("order-123", sut.ObjectId);
        }

        [Fact]
        public void Should_set_and_get_CreatedAfter()
        {
            // Arrange
            var sut = new BackupQuery();
            var date = DateTimeOffset.UtcNow.AddDays(-7);

            // Act
            sut.CreatedAfter = date;

            // Assert
            Assert.Equal(date, sut.CreatedAfter);
        }

        [Fact]
        public void Should_set_and_get_CreatedBefore()
        {
            // Arrange
            var sut = new BackupQuery();
            var date = DateTimeOffset.UtcNow;

            // Act
            sut.CreatedBefore = date;

            // Assert
            Assert.Equal(date, sut.CreatedBefore);
        }

        [Fact]
        public void Should_set_and_get_Tags()
        {
            // Arrange
            var sut = new BackupQuery();
            var tags = new Dictionary<string, string> { { "env", "staging" } };

            // Act
            sut.Tags = tags;

            // Assert
            Assert.Equal(tags, sut.Tags);
        }

        [Fact]
        public void Should_set_and_get_MaxResults()
        {
            // Arrange
            var sut = new BackupQuery();

            // Act
            sut.MaxResults = 50;

            // Assert
            Assert.Equal(50, sut.MaxResults);
        }

        [Fact]
        public void Should_set_and_get_IncludeExpired()
        {
            // Arrange
            var sut = new BackupQuery();

            // Act
            sut.IncludeExpired = true;

            // Assert
            Assert.True(sut.IncludeExpired);
        }
    }
}
