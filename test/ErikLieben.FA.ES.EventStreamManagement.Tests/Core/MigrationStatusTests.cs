using ErikLieben.FA.ES.EventStreamManagement.Core;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Core;

public class MigrationStatusTests
{
    public class EnumValues
    {
        [Fact]
        public void Should_have_pending_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.Pending));
        }

        [Fact]
        public void Should_have_in_progress_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.InProgress));
        }

        [Fact]
        public void Should_have_paused_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.Paused));
        }

        [Fact]
        public void Should_have_verifying_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.Verifying));
        }

        [Fact]
        public void Should_have_cutting_over_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.CuttingOver));
        }

        [Fact]
        public void Should_have_completed_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.Completed));
        }

        [Fact]
        public void Should_have_failed_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.Failed));
        }

        [Fact]
        public void Should_have_cancelled_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.Cancelled));
        }

        [Fact]
        public void Should_have_rolling_back_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.RollingBack));
        }

        [Fact]
        public void Should_have_rolled_back_status()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStatus>(MigrationStatus.RolledBack));
        }
    }

    public class EnumCount
    {
        [Fact]
        public void Should_have_expected_number_of_values()
        {
            // Arrange
            var values = Enum.GetValues<MigrationStatus>();

            // Assert - includes BackingUp status added for backup phase
            Assert.Equal(11, values.Length);
        }
    }
}

public class MigrationStrategyTests
{
    public class EnumValues
    {
        [Fact]
        public void Should_have_copy_and_transform_strategy()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStrategy>(MigrationStrategy.CopyAndTransform));
        }

        [Fact]
        public void Should_have_lazy_transform_strategy()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStrategy>(MigrationStrategy.LazyTransform));
        }

        [Fact]
        public void Should_have_in_place_transform_strategy()
        {
            // Assert
            Assert.True(Enum.IsDefined<MigrationStrategy>(MigrationStrategy.InPlaceTransform));
        }
    }

    public class EnumCount
    {
        [Fact]
        public void Should_have_expected_number_of_values()
        {
            // Arrange
            var values = Enum.GetValues<MigrationStrategy>();

            // Assert
            Assert.Equal(3, values.Length);
        }
    }
}
