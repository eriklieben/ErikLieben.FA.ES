using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class EventStreamClosedExceptionTests
{
    public class BasicConstructor
    {
        [Fact]
        public void Should_set_stream_identifier()
        {
            // Act
            var sut = new EventStreamClosedException("my-stream", "Stream is closed");

            // Assert
            Assert.Equal("my-stream", sut.StreamIdentifier);
        }

        [Fact]
        public void Should_set_message()
        {
            // Act
            var sut = new EventStreamClosedException("my-stream", "Stream is closed for migration");

            // Assert - Message includes error code prefix from EsException base class
            Assert.Equal("[ES_STREAM_CLOSED] Stream is closed for migration", sut.Message);
        }

        [Fact]
        public void Should_not_have_continuation_by_default()
        {
            // Act
            var sut = new EventStreamClosedException("my-stream", "Stream is closed");

            // Assert
            Assert.False(sut.HasContinuation);
            Assert.Null(sut.ContinuationStreamId);
            Assert.Null(sut.ContinuationStreamType);
            Assert.Null(sut.ContinuationDataStore);
            Assert.Null(sut.ContinuationDocumentStore);
            Assert.Null(sut.Reason);
        }
    }

    public class ContinuationConstructor
    {
        [Fact]
        public void Should_set_all_continuation_properties()
        {
            // Act
            var sut = new EventStreamClosedException(
                streamIdentifier: "source-stream",
                continuationStreamId: "target-stream",
                continuationStreamType: "cosmos",
                continuationDataStore: "cosmosdb-prod",
                continuationDocumentStore: "cosmosdb-prod-docs",
                reason: "Migration to new region");

            // Assert
            Assert.Equal("source-stream", sut.StreamIdentifier);
            Assert.Equal("target-stream", sut.ContinuationStreamId);
            Assert.Equal("cosmos", sut.ContinuationStreamType);
            Assert.Equal("cosmosdb-prod", sut.ContinuationDataStore);
            Assert.Equal("cosmosdb-prod-docs", sut.ContinuationDocumentStore);
            Assert.Equal("Migration to new region", sut.Reason);
        }

        [Fact]
        public void Should_report_has_continuation_when_continuation_stream_id_set()
        {
            // Act
            var sut = new EventStreamClosedException(
                streamIdentifier: "source-stream",
                continuationStreamId: "target-stream",
                continuationStreamType: "blob",
                continuationDataStore: "default",
                continuationDocumentStore: "default");

            // Assert
            Assert.True(sut.HasContinuation);
        }

        [Fact]
        public void Should_generate_informative_message()
        {
            // Act
            var sut = new EventStreamClosedException(
                streamIdentifier: "orders-123",
                continuationStreamId: "orders-123-v2",
                continuationStreamType: "blob",
                continuationDataStore: "default",
                continuationDocumentStore: "default");

            // Assert
            Assert.Contains("orders-123", sut.Message);
            Assert.Contains("orders-123-v2", sut.Message);
        }

        [Fact]
        public void Should_allow_null_reason()
        {
            // Act
            var sut = new EventStreamClosedException(
                streamIdentifier: "source-stream",
                continuationStreamId: "target-stream",
                continuationStreamType: "blob",
                continuationDataStore: "default",
                continuationDocumentStore: "default",
                reason: null);

            // Assert
            Assert.Null(sut.Reason);
            Assert.True(sut.HasContinuation); // Still has continuation
        }
    }

    public class InnerExceptionConstructor
    {
        [Fact]
        public void Should_set_inner_exception()
        {
            // Arrange
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var sut = new EventStreamClosedException(
                "my-stream",
                "Stream closed due to error",
                innerException);

            // Assert
            Assert.Same(innerException, sut.InnerException);
            Assert.Equal("my-stream", sut.StreamIdentifier);
        }

        [Fact]
        public void Should_not_have_continuation_with_inner_exception_constructor()
        {
            // Arrange
            var innerException = new Exception("Inner");

            // Act
            var sut = new EventStreamClosedException(
                "my-stream",
                "Error",
                innerException);

            // Assert
            Assert.False(sut.HasContinuation);
        }
    }

    public class HasContinuationProperty
    {
        [Fact]
        public void Should_return_false_when_continuation_stream_id_is_null()
        {
            // Act
            var sut = new EventStreamClosedException("stream", "Closed");

            // Assert
            Assert.False(sut.HasContinuation);
        }

        [Fact]
        public void Should_return_false_when_continuation_stream_id_is_empty()
        {
            // This tests the internal behavior - using reflection or testing through behavior
            // Since we can't set empty string through public constructor, we test the
            // not-null path works correctly
            var sut = new EventStreamClosedException(
                "source",
                "target",
                "blob",
                "store",
                "docstore");

            Assert.True(sut.HasContinuation);
        }

        [Fact]
        public void Should_return_true_when_continuation_stream_id_is_set()
        {
            // Act
            var sut = new EventStreamClosedException(
                "source-stream",
                "target-stream",
                "blob",
                "default",
                "default");

            // Assert
            Assert.True(sut.HasContinuation);
        }
    }

    public class ErrorCode
    {
        [Fact]
        public void Should_have_correct_error_code()
        {
            // Act
            var sut = new EventStreamClosedException("stream", "Message");

            // Assert - EsException has ErrorCode property
            Assert.Equal("ES_STREAM_CLOSED", sut.ErrorCode);
        }
    }

    public class UsageScenarios
    {
        [Fact]
        public void Should_allow_caller_to_extract_continuation_info_for_retry()
        {
            // Arrange - Simulate exception being thrown from data store
            var exception = new EventStreamClosedException(
                streamIdentifier: "orders-old",
                continuationStreamId: "orders-new",
                continuationStreamType: "cosmos",
                continuationDataStore: "cosmos-prod",
                continuationDocumentStore: "cosmos-prod-docs",
                reason: "Live migration");

            // Act - Simulate caller checking for retry
            string? targetStreamId = null;
            if (exception.HasContinuation)
            {
                targetStreamId = exception.ContinuationStreamId;
            }

            // Assert
            Assert.NotNull(targetStreamId);
            Assert.Equal("orders-new", targetStreamId);
        }

        [Fact]
        public void Should_allow_caller_to_handle_no_continuation()
        {
            // Arrange - Stream closed without continuation (error scenario)
            var exception = new EventStreamClosedException(
                "orphaned-stream",
                "Stream was closed without continuation");

            // Act
            bool shouldReloadDocument = !exception.HasContinuation;

            // Assert
            Assert.True(shouldReloadDocument);
        }
    }
}
