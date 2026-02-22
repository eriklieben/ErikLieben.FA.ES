using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests;

public class EventTests
{
    public class TestPayload
    {
        public string Property { get; set; } = string.Empty;
    }

    public class Data
    {

        [Fact]
        public void Should_return_correct_typed_data_when_valid_data_is_provided()
        {
            // Arrange
            var payload = new TestPayload { Property = "TestValue" };
            var jsonEvent = new Event<TestPayload>
            {
                Payload = string.Empty,
                EventVersion = 1,
                EventType = "TestEvent",
                Data = payload
            };

            // Act
            var result = ((IEvent<TestPayload>)jsonEvent).Data();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestValue", result.Property);
        }

        [Fact]
        public void Should_throw_InvalidCastException_when_data_is_not_of_correct_type()
        {
            // Arrange
            var invalidData = new object();
            var jsonEvent = new Event<TestPayload>
            {
                Payload = string.Empty,
                EventVersion = 1,
                EventType = "TestEvent",
                Data = invalidData 
            };

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => ((IEvent<TestPayload>)jsonEvent).Data());
        }
    }
}