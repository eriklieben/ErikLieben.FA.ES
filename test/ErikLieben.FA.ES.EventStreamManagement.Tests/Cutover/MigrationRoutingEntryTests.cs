using ErikLieben.FA.ES.EventStreamManagement.Cutover;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Cutover;

public class MigrationRoutingEntryTests
{
    public class Properties
    {
        [Fact]
        public void Should_have_default_object_id()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry();

            // Assert
            Assert.Equal(string.Empty, sut.ObjectId);
        }

        [Fact]
        public void Should_allow_setting_object_id()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry { ObjectId = "test-object-123" };

            // Assert
            Assert.Equal("test-object-123", sut.ObjectId);
        }

        [Fact]
        public void Should_have_default_phase()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry();

            // Assert
            Assert.Equal(MigrationPhase.Normal, sut.Phase);
        }

        [Fact]
        public void Should_allow_setting_phase()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry { Phase = MigrationPhase.DualWrite };

            // Assert
            Assert.Equal(MigrationPhase.DualWrite, sut.Phase);
        }

        [Fact]
        public void Should_have_default_old_stream()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry();

            // Assert
            Assert.Equal(string.Empty, sut.OldStream);
        }

        [Fact]
        public void Should_allow_setting_old_stream()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry { OldStream = "old-stream-v1" };

            // Assert
            Assert.Equal("old-stream-v1", sut.OldStream);
        }

        [Fact]
        public void Should_have_default_new_stream()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry();

            // Assert
            Assert.Equal(string.Empty, sut.NewStream);
        }

        [Fact]
        public void Should_allow_setting_new_stream()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry { NewStream = "new-stream-v2" };

            // Assert
            Assert.Equal("new-stream-v2", sut.NewStream);
        }

        [Fact]
        public void Should_have_default_created_at()
        {
            // Arrange
            var before = DateTimeOffset.UtcNow;

            // Act
            var sut = new MigrationRoutingEntry();
            var after = DateTimeOffset.UtcNow;

            // Assert
            Assert.True(sut.CreatedAt >= before && sut.CreatedAt <= after);
        }

        [Fact]
        public void Should_allow_setting_created_at()
        {
            // Arrange
            var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

            // Act
            var sut = new MigrationRoutingEntry { CreatedAt = timestamp };

            // Assert
            Assert.Equal(timestamp, sut.CreatedAt);
        }

        [Fact]
        public void Should_have_default_updated_at()
        {
            // Arrange
            var before = DateTimeOffset.UtcNow;

            // Act
            var sut = new MigrationRoutingEntry();
            var after = DateTimeOffset.UtcNow;

            // Assert
            Assert.True(sut.UpdatedAt >= before && sut.UpdatedAt <= after);
        }

        [Fact]
        public void Should_allow_setting_updated_at()
        {
            // Arrange
            var timestamp = new DateTimeOffset(2024, 6, 20, 14, 45, 0, TimeSpan.Zero);

            // Act
            var sut = new MigrationRoutingEntry { UpdatedAt = timestamp };

            // Assert
            Assert.Equal(timestamp, sut.UpdatedAt);
        }

        [Fact]
        public void Should_have_default_migration_id()
        {
            // Arrange & Act
            var sut = new MigrationRoutingEntry();

            // Assert
            Assert.Equal(Guid.Empty, sut.MigrationId);
        }

        [Fact]
        public void Should_allow_setting_migration_id()
        {
            // Arrange
            var migrationId = Guid.NewGuid();

            // Act
            var sut = new MigrationRoutingEntry { MigrationId = migrationId };

            // Assert
            Assert.Equal(migrationId, sut.MigrationId);
        }
    }

    public class ToStreamRoutingMethod
    {
        [Fact]
        public void Should_return_normal_routing_for_normal_phase()
        {
            // Arrange
            var sut = new MigrationRoutingEntry
            {
                Phase = MigrationPhase.Normal,
                OldStream = "stream-v1"
            };

            // Act
            var result = sut.ToStreamRouting();

            // Assert
            Assert.Equal("stream-v1", result.PrimaryReadStream);
            Assert.Equal("stream-v1", result.PrimaryWriteStream);
            Assert.False(result.IsDualWriteActive);
            Assert.False(result.IsDualReadActive);
        }

        [Fact]
        public void Should_return_dual_write_routing_for_dual_write_phase()
        {
            // Arrange
            var sut = new MigrationRoutingEntry
            {
                Phase = MigrationPhase.DualWrite,
                OldStream = "stream-v1",
                NewStream = "stream-v2"
            };

            // Act
            var result = sut.ToStreamRouting();

            // Assert
            Assert.Equal("stream-v1", result.PrimaryReadStream);
            Assert.Equal("stream-v1", result.PrimaryWriteStream);
            Assert.Equal("stream-v2", result.SecondaryWriteStream);
            Assert.True(result.IsDualWriteActive);
        }

        [Fact]
        public void Should_return_dual_read_routing_for_dual_read_phase()
        {
            // Arrange
            var sut = new MigrationRoutingEntry
            {
                Phase = MigrationPhase.DualRead,
                OldStream = "stream-v1",
                NewStream = "stream-v2"
            };

            // Act
            var result = sut.ToStreamRouting();

            // Assert
            Assert.Equal("stream-v2", result.PrimaryWriteStream);
            Assert.True(result.IsDualReadActive);
        }

        [Fact]
        public void Should_return_cutover_routing_for_cutover_phase()
        {
            // Arrange
            var sut = new MigrationRoutingEntry
            {
                Phase = MigrationPhase.Cutover,
                OldStream = "stream-v1",
                NewStream = "stream-v2"
            };

            // Act
            var result = sut.ToStreamRouting();

            // Assert
            Assert.Equal("stream-v2", result.PrimaryReadStream);
            Assert.Equal("stream-v2", result.PrimaryWriteStream);
            Assert.False(result.IsDualWriteActive);
            Assert.False(result.IsDualReadActive);
        }

        [Fact]
        public void Should_return_cutover_routing_for_book_closed_phase()
        {
            // Arrange
            var sut = new MigrationRoutingEntry
            {
                Phase = MigrationPhase.BookClosed,
                OldStream = "stream-v1",
                NewStream = "stream-v2"
            };

            // Act
            var result = sut.ToStreamRouting();

            // Assert
            Assert.Equal("stream-v2", result.PrimaryReadStream);
            Assert.Equal("stream-v2", result.PrimaryWriteStream);
        }

        [Theory]
        [InlineData(MigrationPhase.Normal)]
        [InlineData(MigrationPhase.DualWrite)]
        [InlineData(MigrationPhase.DualRead)]
        [InlineData(MigrationPhase.Cutover)]
        [InlineData(MigrationPhase.BookClosed)]
        public void Should_return_valid_routing_for_all_phases(MigrationPhase phase)
        {
            // Arrange
            var sut = new MigrationRoutingEntry
            {
                Phase = phase,
                OldStream = "old",
                NewStream = "new"
            };

            // Act
            var result = sut.ToStreamRouting();

            // Assert
            Assert.NotNull(result);
        }
    }

    public class FullEntryTests
    {
        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange
            var migrationId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
            var updatedAt = DateTimeOffset.UtcNow;

            // Act
            var sut = new MigrationRoutingEntry
            {
                ObjectId = "object-123",
                Phase = MigrationPhase.DualWrite,
                OldStream = "stream-v1",
                NewStream = "stream-v2",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                MigrationId = migrationId
            };

            // Assert
            Assert.Equal("object-123", sut.ObjectId);
            Assert.Equal(MigrationPhase.DualWrite, sut.Phase);
            Assert.Equal("stream-v1", sut.OldStream);
            Assert.Equal("stream-v2", sut.NewStream);
            Assert.Equal(createdAt, sut.CreatedAt);
            Assert.Equal(updatedAt, sut.UpdatedAt);
            Assert.Equal(migrationId, sut.MigrationId);
        }
    }
}
