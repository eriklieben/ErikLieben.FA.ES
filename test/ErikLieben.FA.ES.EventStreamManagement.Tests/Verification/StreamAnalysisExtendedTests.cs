using ErikLieben.FA.ES.EventStreamManagement.Verification;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Verification;

public class StreamAnalysisExtendedTests
{
    public class AllProperties
    {
        [Fact]
        public void Should_have_event_type_distribution()
        {
            // Arrange
            var distribution = new Dictionary<string, long>
            {
                { "OrderCreated", 100 },
                { "OrderUpdated", 50 }
            };

            // Act
            var sut = new StreamAnalysis { EventTypeDistribution = distribution };

            // Assert
            Assert.Equal(2, sut.EventTypeDistribution.Count);
            Assert.Equal(100, sut.EventTypeDistribution["OrderCreated"]);
        }

        [Fact]
        public void Should_have_default_event_type_distribution()
        {
            // Arrange & Act
            var sut = new StreamAnalysis();

            // Assert
            Assert.NotNull(sut.EventTypeDistribution);
            Assert.Empty(sut.EventTypeDistribution);
        }

        [Fact]
        public void Should_have_earliest_event()
        {
            // Arrange
            var timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // Act
            var sut = new StreamAnalysis { EarliestEvent = timestamp };

            // Assert
            Assert.Equal(timestamp, sut.EarliestEvent);
        }

        [Fact]
        public void Should_have_latest_event()
        {
            // Arrange
            var timestamp = new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);

            // Act
            var sut = new StreamAnalysis { LatestEvent = timestamp };

            // Assert
            Assert.Equal(timestamp, sut.LatestEvent);
        }

        [Fact]
        public void Should_have_current_version()
        {
            // Arrange & Act
            var sut = new StreamAnalysis { CurrentVersion = 1500 };

            // Assert
            Assert.Equal(1500, sut.CurrentVersion);
        }

        [Fact]
        public void Should_allow_null_earliest_event()
        {
            // Arrange & Act
            var sut = new StreamAnalysis { EarliestEvent = null };

            // Assert
            Assert.Null(sut.EarliestEvent);
        }

        [Fact]
        public void Should_allow_null_latest_event()
        {
            // Arrange & Act
            var sut = new StreamAnalysis { LatestEvent = null };

            // Assert
            Assert.Null(sut.LatestEvent);
        }

        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange
            var distribution = new Dictionary<string, long>
            {
                { "TestEvent", 10 }
            };
            var earliestEvent = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var latestEvent = new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero);

            // Act
            var sut = new StreamAnalysis
            {
                EventCount = 1000,
                SizeBytes = 1024 * 1024,
                EventTypeDistribution = distribution,
                EarliestEvent = earliestEvent,
                LatestEvent = latestEvent,
                CurrentVersion = 999
            };

            // Assert
            Assert.Equal(1000, sut.EventCount);
            Assert.Equal(1024 * 1024, sut.SizeBytes);
            Assert.Equal(distribution, sut.EventTypeDistribution);
            Assert.Equal(earliestEvent, sut.EarliestEvent);
            Assert.Equal(latestEvent, sut.LatestEvent);
            Assert.Equal(999, sut.CurrentVersion);
        }
    }
}

public class ResourceEstimateExtendedTests
{
    public class AllProperties
    {
        [Fact]
        public void Should_have_estimated_storage_bytes()
        {
            // Arrange & Act
            var sut = new ResourceEstimate { EstimatedStorageBytes = 1024 * 1024 * 100 };

            // Assert
            Assert.Equal(1024 * 1024 * 100, sut.EstimatedStorageBytes);
        }

        [Fact]
        public void Should_have_estimated_cost()
        {
            // Arrange & Act
            var sut = new ResourceEstimate { EstimatedCost = 99.99m };

            // Assert
            Assert.Equal(99.99m, sut.EstimatedCost);
        }

        [Fact]
        public void Should_allow_null_estimated_cost()
        {
            // Arrange & Act
            var sut = new ResourceEstimate { EstimatedCost = null };

            // Assert
            Assert.Null(sut.EstimatedCost);
        }

        [Fact]
        public void Should_have_estimated_bandwidth_bytes()
        {
            // Arrange & Act
            var sut = new ResourceEstimate { EstimatedBandwidthBytes = 1024 * 1024 * 50 };

            // Assert
            Assert.Equal(1024 * 1024 * 50, sut.EstimatedBandwidthBytes);
        }

        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange & Act
            var sut = new ResourceEstimate
            {
                EstimatedDuration = TimeSpan.FromHours(2),
                EstimatedStorageBytes = 1024 * 1024 * 500,
                EstimatedCost = 150.00m,
                EstimatedBandwidthBytes = 1024 * 1024 * 1024
            };

            // Assert
            Assert.Equal(TimeSpan.FromHours(2), sut.EstimatedDuration);
            Assert.Equal(1024 * 1024 * 500, sut.EstimatedStorageBytes);
            Assert.Equal(150.00m, sut.EstimatedCost);
            Assert.Equal(1024 * 1024 * 1024, sut.EstimatedBandwidthBytes);
        }
    }
}

public class PrerequisiteExtendedTests
{
    public class AllProperties
    {
        [Fact]
        public void Should_have_is_met()
        {
            // Arrange & Act
            var sut = new Prerequisite
            {
                Name = "Backup",
                Description = "Backup required",
                IsMet = true
            };

            // Assert
            Assert.True(sut.IsMet);
        }

        [Fact]
        public void Should_have_is_blocking()
        {
            // Arrange & Act
            var sut = new Prerequisite
            {
                Name = "Backup",
                Description = "Backup required",
                IsBlocking = true
            };

            // Assert
            Assert.True(sut.IsBlocking);
        }

        [Fact]
        public void Should_default_is_met_to_false()
        {
            // Arrange & Act
            var sut = new Prerequisite
            {
                Name = "Test",
                Description = "Test description"
            };

            // Assert
            Assert.False(sut.IsMet);
        }

        [Fact]
        public void Should_default_is_blocking_to_false()
        {
            // Arrange & Act
            var sut = new Prerequisite
            {
                Name = "Test",
                Description = "Test description"
            };

            // Assert
            Assert.False(sut.IsBlocking);
        }
    }
}

public class MigrationRiskExtendedTests
{
    public class AllProperties
    {
        [Fact]
        public void Should_have_mitigations()
        {
            // Arrange
            var mitigations = new List<string>
            {
                "Create backup",
                "Test in staging first"
            };

            // Act
            var sut = new MigrationRisk
            {
                Category = "Data",
                Description = "Risk of data loss",
                Severity = "High",
                Mitigations = mitigations
            };

            // Assert
            Assert.Equal(2, sut.Mitigations.Count);
            Assert.Contains("Create backup", sut.Mitigations);
        }

        [Fact]
        public void Should_have_default_empty_mitigations()
        {
            // Arrange & Act
            var sut = new MigrationRisk
            {
                Category = "Test",
                Description = "Test description",
                Severity = "Low"
            };

            // Assert
            Assert.NotNull(sut.Mitigations);
            Assert.Empty(sut.Mitigations);
        }
    }
}

public class TransformationSimulationExtendedTests
{
    public class AllProperties
    {
        [Fact]
        public void Should_have_successful_transformations()
        {
            // Arrange & Act
            var sut = new TransformationSimulation { SuccessfulTransformations = 95 };

            // Assert
            Assert.Equal(95, sut.SuccessfulTransformations);
        }

        [Fact]
        public void Should_have_failed_transformations()
        {
            // Arrange & Act
            var sut = new TransformationSimulation { FailedTransformations = 5 };

            // Assert
            Assert.Equal(5, sut.FailedTransformations);
        }

        [Fact]
        public void Should_have_failures_list()
        {
            // Arrange
            var failures = new List<TransformationFailure>
            {
                new TransformationFailure { EventVersion = 1, EventName = "TestEvent", Error = "Test error" }
            };

            // Act
            var sut = new TransformationSimulation { Failures = failures };

            // Assert
            Assert.Single(sut.Failures);
        }

        [Fact]
        public void Should_have_default_empty_failures()
        {
            // Arrange & Act
            var sut = new TransformationSimulation();

            // Assert
            Assert.NotNull(sut.Failures);
            Assert.Empty(sut.Failures);
        }

        [Fact]
        public void Should_have_average_transform_time()
        {
            // Arrange & Act
            var sut = new TransformationSimulation { AverageTransformTime = TimeSpan.FromMilliseconds(50) };

            // Assert
            Assert.Equal(TimeSpan.FromMilliseconds(50), sut.AverageTransformTime);
        }

        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange
            var failures = new List<TransformationFailure>
            {
                new TransformationFailure { EventVersion = 5, EventName = "FailedEvent", Error = "Parse error" }
            };

            // Act
            var sut = new TransformationSimulation
            {
                SampleSize = 100,
                SuccessfulTransformations = 99,
                FailedTransformations = 1,
                Failures = failures,
                AverageTransformTime = TimeSpan.FromMilliseconds(25)
            };

            // Assert
            Assert.Equal(100, sut.SampleSize);
            Assert.Equal(99, sut.SuccessfulTransformations);
            Assert.Equal(1, sut.FailedTransformations);
            Assert.Single(sut.Failures);
            Assert.Equal(TimeSpan.FromMilliseconds(25), sut.AverageTransformTime);
        }
    }
}
