using ErikLieben.FA.ES.Results;

namespace ErikLieben.FA.ES.Tests.Results;

public class EventSourcingErrorsTests
{
    public class StreamNotFoundMethod
    {
        [Fact]
        public void Should_create_error_with_stream_id()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.StreamNotFound("stream-123");

            // Assert
            Assert.Equal("STREAM_NOT_FOUND", sut.Code);
            Assert.Contains("stream-123", sut.Message);
        }
    }

    public class ConcurrencyConflictMethod
    {
        [Fact]
        public void Should_create_error_with_version_details()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.ConcurrencyConflict("stream-123", expectedVersion: 5, actualVersion: 7);

            // Assert
            Assert.Equal("CONCURRENCY_CONFLICT", sut.Code);
            Assert.Contains("stream-123", sut.Message);
            Assert.Contains("5", sut.Message);
            Assert.Contains("7", sut.Message);
        }
    }

    public class AggregateNotFoundMethod
    {
        [Fact]
        public void Should_create_error_with_type_and_id()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.AggregateNotFound("Order", "order-456");

            // Assert
            Assert.Equal("AGGREGATE_NOT_FOUND", sut.Code);
            Assert.Contains("Order", sut.Message);
            Assert.Contains("order-456", sut.Message);
        }
    }

    public class AggregateAlreadyExistsMethod
    {
        [Fact]
        public void Should_create_error_with_type_and_id()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.AggregateAlreadyExists("Order", "order-456");

            // Assert
            Assert.Equal("AGGREGATE_ALREADY_EXISTS", sut.Code);
            Assert.Contains("Order", sut.Message);
            Assert.Contains("order-456", sut.Message);
        }
    }

    public class EventDeserializationFailedMethod
    {
        [Fact]
        public void Should_create_error_with_event_type()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.EventDeserializationFailed("OrderCreated");

            // Assert
            Assert.Equal("EVENT_DESERIALIZATION_FAILED", sut.Code);
            Assert.Contains("OrderCreated", sut.Message);
        }
    }

    public class ProjectionNotFoundMethod
    {
        [Fact]
        public void Should_create_error_with_type_and_id()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.ProjectionNotFound("OrderDashboard", "dash-1");

            // Assert
            Assert.Equal("PROJECTION_NOT_FOUND", sut.Code);
            Assert.Contains("OrderDashboard", sut.Message);
            Assert.Contains("dash-1", sut.Message);
        }
    }

    public class ProjectionSaveFailedMethod
    {
        [Fact]
        public void Should_create_error_with_type_and_message()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.ProjectionSaveFailed("OrderDashboard", "Blob conflict");

            // Assert
            Assert.Equal("PROJECTION_SAVE_FAILED", sut.Code);
            Assert.Contains("OrderDashboard", sut.Message);
            Assert.Contains("Blob conflict", sut.Message);
        }
    }

    public class SnapshotNotFoundMethod
    {
        [Fact]
        public void Should_create_error_with_stream_id_and_version()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.SnapshotNotFound("stream-123", 10);

            // Assert
            Assert.Equal("SNAPSHOT_NOT_FOUND", sut.Code);
            Assert.Contains("stream-123", sut.Message);
            Assert.Contains("10", sut.Message);
        }
    }

    public class StorageOperationFailedMethod
    {
        [Fact]
        public void Should_create_error_with_operation_and_message()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.StorageOperationFailed("WriteBlob", "Timeout reached");

            // Assert
            Assert.Equal("STORAGE_OPERATION_FAILED", sut.Code);
            Assert.Contains("WriteBlob", sut.Message);
            Assert.Contains("Timeout reached", sut.Message);
        }
    }

    public class OperationCancelledField
    {
        [Fact]
        public void Should_have_operation_cancelled_code()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.OperationCancelled;

            // Assert
            Assert.Equal("OPERATION_CANCELLED", sut.Code);
            Assert.NotEmpty(sut.Message);
        }
    }

    public class TimeoutMethod
    {
        [Fact]
        public void Should_create_error_with_duration()
        {
            // Arrange
            var duration = TimeSpan.FromSeconds(30);

            // Act
            var sut = EventSourcingErrors.Timeout(duration);

            // Assert
            Assert.Equal("TIMEOUT", sut.Code);
            Assert.Contains("30", sut.Message);
            Assert.Contains("seconds", sut.Message);
        }
    }

    public class ValidationFailedMethod
    {
        [Fact]
        public void Should_create_error_with_message()
        {
            // Arrange & Act
            var sut = EventSourcingErrors.ValidationFailed("Email is required");

            // Assert
            Assert.Equal("VALIDATION_FAILED", sut.Code);
            Assert.Equal("Email is required", sut.Message);
        }
    }
}
