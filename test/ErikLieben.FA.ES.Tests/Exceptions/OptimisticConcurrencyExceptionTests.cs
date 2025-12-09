using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class OptimisticConcurrencyExceptionTests
{
    public class MessageConstructor
    {
        [Fact]
        public void Should_set_stream_identifier()
        {
            // Act
            var sut = new OptimisticConcurrencyException("my-stream", "Conflict occurred");

            // Assert
            Assert.Equal("my-stream", sut.StreamIdentifier);
        }

        [Fact]
        public void Should_set_message()
        {
            // Act
            var sut = new OptimisticConcurrencyException("my-stream", "Version mismatch detected");

            // Assert - Message includes error code prefix from EsException base class
            Assert.Equal("[ES_CONCURRENCY_CONFLICT] Version mismatch detected", sut.Message);
        }

        [Fact]
        public void Should_not_have_version_info()
        {
            // Act
            var sut = new OptimisticConcurrencyException("my-stream", "Conflict");

            // Assert
            Assert.Null(sut.ExpectedVersion);
            Assert.Null(sut.ActualVersion);
        }
    }

    public class VersionConstructor
    {
        [Fact]
        public void Should_set_expected_version()
        {
            // Act
            var sut = new OptimisticConcurrencyException("my-stream", 5, 7);

            // Assert
            Assert.Equal(5, sut.ExpectedVersion);
        }

        [Fact]
        public void Should_set_actual_version()
        {
            // Act
            var sut = new OptimisticConcurrencyException("my-stream", 5, 7);

            // Assert
            Assert.Equal(7, sut.ActualVersion);
        }

        [Fact]
        public void Should_generate_informative_message()
        {
            // Act
            var sut = new OptimisticConcurrencyException("orders-123", 10, 15);

            // Assert
            Assert.Contains("orders-123", sut.Message);
            Assert.Contains("10", sut.Message);
            Assert.Contains("15", sut.Message);
        }

        [Fact]
        public void Should_set_stream_identifier()
        {
            // Act
            var sut = new OptimisticConcurrencyException("my-stream", 1, 2);

            // Assert
            Assert.Equal("my-stream", sut.StreamIdentifier);
        }
    }

    public class InnerExceptionConstructor
    {
        [Fact]
        public void Should_set_inner_exception()
        {
            // Arrange
            var inner = new InvalidOperationException("Storage conflict");

            // Act
            var sut = new OptimisticConcurrencyException("my-stream", "Conflict", inner);

            // Assert
            Assert.Same(inner, sut.InnerException);
        }

        [Fact]
        public void Should_set_stream_identifier()
        {
            // Arrange
            var inner = new Exception("Inner");

            // Act
            var sut = new OptimisticConcurrencyException("my-stream", "Conflict", inner);

            // Assert
            Assert.Equal("my-stream", sut.StreamIdentifier);
        }

        [Fact]
        public void Should_set_message()
        {
            // Arrange
            var inner = new Exception("Inner");

            // Act
            var sut = new OptimisticConcurrencyException("my-stream", "ETag mismatch", inner);

            // Assert - Message includes error code prefix from EsException base class
            Assert.Equal("[ES_CONCURRENCY_CONFLICT] ETag mismatch", sut.Message);
        }
    }

    public class ErrorCode
    {
        [Fact]
        public void Should_have_correct_error_code()
        {
            // Act
            var sut = new OptimisticConcurrencyException("stream", "Message");

            // Assert
            Assert.Equal("ES_CONCURRENCY_CONFLICT", sut.ErrorCode);
        }
    }

    public class UsageScenarios
    {
        [Fact]
        public void Should_provide_version_info_for_retry_logic()
        {
            // Arrange
            var exception = new OptimisticConcurrencyException("orders-123", 50, 55);

            // Act - Caller can use actual version to decide how to retry
            int eventsToReread = exception.ActualVersion!.Value - exception.ExpectedVersion!.Value;

            // Assert
            Assert.Equal(5, eventsToReread);
        }

        [Fact]
        public void Should_allow_wrapping_storage_exceptions()
        {
            // Arrange - Simulate Azure Storage ETag conflict
            var storageException = new InvalidOperationException("412 Precondition Failed");

            // Act
            var sut = new OptimisticConcurrencyException(
                "my-stream",
                "Storage conflict during append",
                storageException);

            // Assert
            Assert.IsType<InvalidOperationException>(sut.InnerException);
            Assert.Equal("my-stream", sut.StreamIdentifier);
        }
    }
}
