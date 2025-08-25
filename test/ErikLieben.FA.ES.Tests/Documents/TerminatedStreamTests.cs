using System;
using Xunit;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Tests.Documents
{
    public class TerminatedStreamTests
    {
        public class Properties
        {
            [Fact]
            public void Should_set_and_get_stream_identifier()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = "stream-123";

                // Act
                sut.StreamIdentifier = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.StreamIdentifier);
            }

            [Fact]
            public void Should_set_and_get_stream_type()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = "test-stream-type";

                // Act
                sut.StreamType = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.StreamType);
            }

            [Fact]
            public void Should_set_and_get_stream_connection_name()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = "test-connection";

                // Act
                sut.StreamConnectionName = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.StreamConnectionName);
            }

            [Fact]
            public void Should_set_and_get_reason()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = "Stream terminated due to inactivity";

                // Act
                sut.Reason = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.Reason);
            }

            [Fact]
            public void Should_set_and_get_continuation_stream_id()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = "continuation-stream-456";

                // Act
                sut.ContinuationStreamId = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.ContinuationStreamId);
            }

            [Fact]
            public void Should_set_and_get_termination_date()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = DateTimeOffset.UtcNow;

                // Act
                sut.TerminationDate = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.TerminationDate);
            }

            [Fact]
            public void Should_set_and_get_stream_version()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = 42;

                // Act
                sut.StreamVersion = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.StreamVersion);
            }

            [Fact]
            public void Should_set_and_get_deleted_flag()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = true;

                // Act
                sut.Deleted = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.Deleted);
            }

            [Fact]
            public void Should_set_and_get_deletion_date()
            {
                // Arrange
                var sut = new TerminatedStream();
                var expectedValue = DateTimeOffset.UtcNow;

                // Act
                sut.DeletionDate = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.DeletionDate);
            }
        }

        public class Initialization
        {
            [Fact]
            public void Should_initialize_with_default_values()
            {
                // Arrange & Act
                var sut = new TerminatedStream();

                // Assert
                Assert.Null(sut.StreamIdentifier);
                Assert.Null(sut.StreamType);
                Assert.Null(sut.StreamConnectionName);
                Assert.Null(sut.Reason);
                Assert.Null(sut.ContinuationStreamId);
                Assert.Equal(default, sut.TerminationDate);
                Assert.Null(sut.StreamVersion);
                Assert.False(sut.Deleted);
                Assert.Equal(default, sut.DeletionDate);
            }
        }

        public class RecordFunctionality
        {
            [Fact]
            public void Should_support_with_expressions()
            {
                // Arrange
                var original = new TerminatedStream
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "test-type",
                    StreamConnectionName = "connection-1",
                    Reason = "Test reason",
                    ContinuationStreamId = "continuation-456",
                    TerminationDate = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    StreamVersion = 5,
                    Deleted = false,
                    DeletionDate = new DateTimeOffset(2023, 1, 2, 0, 0, 0, TimeSpan.Zero)
                };

                // Act
                var modified = original with { Deleted = true, Reason = "Updated reason" };

                // Assert
                Assert.Equal(original.StreamIdentifier, modified.StreamIdentifier);
                Assert.Equal(original.StreamType, modified.StreamType);
                Assert.Equal(original.StreamConnectionName, modified.StreamConnectionName);
                Assert.Equal("Updated reason", modified.Reason);
                Assert.Equal(original.ContinuationStreamId, modified.ContinuationStreamId);
                Assert.Equal(original.TerminationDate, modified.TerminationDate);
                Assert.Equal(original.StreamVersion, modified.StreamVersion);
                Assert.True(modified.Deleted);
                Assert.Equal(original.DeletionDate, modified.DeletionDate);
            }

            [Fact]
            public void Should_implement_value_equality()
            {
                // Arrange
                var date1 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
                var date2 = new DateTimeOffset(2023, 1, 2, 0, 0, 0, TimeSpan.Zero);

                var instance1 = new TerminatedStream
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "test-type",
                    StreamConnectionName = "connection-1",
                    Reason = "Test reason",
                    ContinuationStreamId = "continuation-456",
                    TerminationDate = date1,
                    StreamVersion = 5,
                    Deleted = true,
                    DeletionDate = date2
                };

                var instance2 = new TerminatedStream
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "test-type",
                    StreamConnectionName = "connection-1",
                    Reason = "Test reason",
                    ContinuationStreamId = "continuation-456",
                    TerminationDate = date1,
                    StreamVersion = 5,
                    Deleted = true,
                    DeletionDate = date2
                };

                var differentInstance = new TerminatedStream
                {
                    StreamIdentifier = "different-stream",
                    StreamType = "test-type",
                    StreamConnectionName = "connection-1",
                    Reason = "Test reason",
                    ContinuationStreamId = "continuation-456",
                    TerminationDate = date1,
                    StreamVersion = 5,
                    Deleted = true,
                    DeletionDate = date2
                };

                // Act & Assert
                Assert.Equal(instance1, instance2);
                Assert.NotEqual(instance1, differentInstance);
                
                // Verify hash codes
                Assert.Equal(instance1.GetHashCode(), instance2.GetHashCode());
                Assert.NotEqual(instance1.GetHashCode(), differentInstance.GetHashCode());
            }
        }
    }
}