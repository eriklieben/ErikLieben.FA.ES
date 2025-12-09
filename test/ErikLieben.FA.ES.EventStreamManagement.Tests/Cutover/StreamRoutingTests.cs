using ErikLieben.FA.ES.EventStreamManagement.Cutover;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Cutover;

public class StreamRoutingTests
{
    public class RecordProperties
    {
        [Fact]
        public void Should_have_phase_property()
        {
            // Arrange & Act
            var sut = new StreamRouting(
                MigrationPhase.Normal,
                "read-stream",
                "write-stream");

            // Assert
            Assert.Equal(MigrationPhase.Normal, sut.Phase);
        }

        [Fact]
        public void Should_have_primary_read_stream_property()
        {
            // Arrange & Act
            var sut = new StreamRouting(
                MigrationPhase.Normal,
                "primary-read",
                "primary-write");

            // Assert
            Assert.Equal("primary-read", sut.PrimaryReadStream);
        }

        [Fact]
        public void Should_have_primary_write_stream_property()
        {
            // Arrange & Act
            var sut = new StreamRouting(
                MigrationPhase.Normal,
                "primary-read",
                "primary-write");

            // Assert
            Assert.Equal("primary-write", sut.PrimaryWriteStream);
        }

        [Fact]
        public void Should_have_null_secondary_streams_by_default()
        {
            // Arrange & Act
            var sut = new StreamRouting(
                MigrationPhase.Normal,
                "read",
                "write");

            // Assert
            Assert.Null(sut.SecondaryReadStream);
            Assert.Null(sut.SecondaryWriteStream);
        }

        [Fact]
        public void Should_allow_setting_secondary_streams()
        {
            // Arrange & Act
            var sut = new StreamRouting(
                MigrationPhase.DualWrite,
                "read",
                "write",
                "secondary-read",
                "secondary-write");

            // Assert
            Assert.Equal("secondary-read", sut.SecondaryReadStream);
            Assert.Equal("secondary-write", sut.SecondaryWriteStream);
        }
    }

    public class IsDualWriteActiveProperty
    {
        [Fact]
        public void Should_return_false_when_no_secondary_write_stream()
        {
            // Arrange
            var sut = new StreamRouting(
                MigrationPhase.Normal,
                "read",
                "write");

            // Act & Assert
            Assert.False(sut.IsDualWriteActive);
        }

        [Fact]
        public void Should_return_true_when_secondary_write_stream_exists()
        {
            // Arrange
            var sut = new StreamRouting(
                MigrationPhase.DualWrite,
                "read",
                "write",
                null,
                "secondary-write");

            // Act & Assert
            Assert.True(sut.IsDualWriteActive);
        }
    }

    public class IsDualReadActiveProperty
    {
        [Fact]
        public void Should_return_false_when_no_secondary_read_stream()
        {
            // Arrange
            var sut = new StreamRouting(
                MigrationPhase.Normal,
                "read",
                "write");

            // Act & Assert
            Assert.False(sut.IsDualReadActive);
        }

        [Fact]
        public void Should_return_true_when_secondary_read_stream_exists()
        {
            // Arrange
            var sut = new StreamRouting(
                MigrationPhase.DualRead,
                "read",
                "write",
                "secondary-read",
                null);

            // Act & Assert
            Assert.True(sut.IsDualReadActive);
        }
    }

    public class NormalFactoryMethod
    {
        [Fact]
        public void Should_create_normal_routing()
        {
            // Arrange
            const string streamId = "my-stream";

            // Act
            var sut = StreamRouting.Normal(streamId);

            // Assert
            Assert.Equal(MigrationPhase.Normal, sut.Phase);
            Assert.Equal(streamId, sut.PrimaryReadStream);
            Assert.Equal(streamId, sut.PrimaryWriteStream);
            Assert.Null(sut.SecondaryReadStream);
            Assert.Null(sut.SecondaryWriteStream);
        }

        [Fact]
        public void Should_not_have_dual_write_active()
        {
            // Arrange & Act
            var sut = StreamRouting.Normal("stream");

            // Assert
            Assert.False(sut.IsDualWriteActive);
        }

        [Fact]
        public void Should_not_have_dual_read_active()
        {
            // Arrange & Act
            var sut = StreamRouting.Normal("stream");

            // Assert
            Assert.False(sut.IsDualReadActive);
        }
    }

    public class DualWriteFactoryMethod
    {
        [Fact]
        public void Should_create_dual_write_routing()
        {
            // Arrange
            const string oldStream = "old-stream";
            const string newStream = "new-stream";

            // Act
            var sut = StreamRouting.DualWrite(oldStream, newStream);

            // Assert
            Assert.Equal(MigrationPhase.DualWrite, sut.Phase);
            Assert.Equal(oldStream, sut.PrimaryReadStream);
            Assert.Equal(oldStream, sut.PrimaryWriteStream);
            Assert.Null(sut.SecondaryReadStream);
            Assert.Equal(newStream, sut.SecondaryWriteStream);
        }

        [Fact]
        public void Should_have_dual_write_active()
        {
            // Arrange & Act
            var sut = StreamRouting.DualWrite("old", "new");

            // Assert
            Assert.True(sut.IsDualWriteActive);
        }

        [Fact]
        public void Should_not_have_dual_read_active()
        {
            // Arrange & Act
            var sut = StreamRouting.DualWrite("old", "new");

            // Assert
            Assert.False(sut.IsDualReadActive);
        }
    }

    public class DualReadFactoryMethod
    {
        [Fact]
        public void Should_create_dual_read_routing()
        {
            // Arrange
            const string oldStream = "old-stream";
            const string newStream = "new-stream";

            // Act
            var sut = StreamRouting.DualRead(oldStream, newStream);

            // Assert
            Assert.Equal(MigrationPhase.DualRead, sut.Phase);
            Assert.Equal(newStream, sut.PrimaryReadStream);
            Assert.Equal(newStream, sut.PrimaryWriteStream);
            Assert.Equal(oldStream, sut.SecondaryReadStream);
            Assert.Equal(oldStream, sut.SecondaryWriteStream);
        }

        [Fact]
        public void Should_have_dual_write_active()
        {
            // Arrange & Act
            var sut = StreamRouting.DualRead("old", "new");

            // Assert
            Assert.True(sut.IsDualWriteActive);
        }

        [Fact]
        public void Should_have_dual_read_active()
        {
            // Arrange & Act
            var sut = StreamRouting.DualRead("old", "new");

            // Assert
            Assert.True(sut.IsDualReadActive);
        }
    }

    public class CutoverFactoryMethod
    {
        [Fact]
        public void Should_create_cutover_routing()
        {
            // Arrange
            const string newStream = "new-stream";

            // Act
            var sut = StreamRouting.Cutover(newStream);

            // Assert
            Assert.Equal(MigrationPhase.Cutover, sut.Phase);
            Assert.Equal(newStream, sut.PrimaryReadStream);
            Assert.Equal(newStream, sut.PrimaryWriteStream);
            Assert.Null(sut.SecondaryReadStream);
            Assert.Null(sut.SecondaryWriteStream);
        }

        [Fact]
        public void Should_not_have_dual_write_active()
        {
            // Arrange & Act
            var sut = StreamRouting.Cutover("new");

            // Assert
            Assert.False(sut.IsDualWriteActive);
        }

        [Fact]
        public void Should_not_have_dual_read_active()
        {
            // Arrange & Act
            var sut = StreamRouting.Cutover("new");

            // Assert
            Assert.False(sut.IsDualReadActive);
        }
    }

    public class RecordEquality
    {
        [Fact]
        public void Should_be_equal_when_same_values()
        {
            // Arrange
            var sut1 = new StreamRouting(MigrationPhase.Normal, "read", "write");
            var sut2 = new StreamRouting(MigrationPhase.Normal, "read", "write");

            // Act & Assert
            Assert.Equal(sut1, sut2);
        }

        [Fact]
        public void Should_not_be_equal_when_different_values()
        {
            // Arrange
            var sut1 = new StreamRouting(MigrationPhase.Normal, "read", "write");
            var sut2 = new StreamRouting(MigrationPhase.DualWrite, "read", "write");

            // Act & Assert
            Assert.NotEqual(sut1, sut2);
        }
    }
}
