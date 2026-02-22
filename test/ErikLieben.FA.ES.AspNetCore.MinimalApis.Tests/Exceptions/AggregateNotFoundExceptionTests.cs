#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests.Exceptions;

public class AggregateNotFoundExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultMessage()
    {
        // Act
        var exception = new AggregateNotFoundException();

        // Assert
        Assert.Equal("The requested aggregate was not found.", exception.Message);
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        // Arrange
        const string message = "Custom message";

        // Act
        var exception = new AggregateNotFoundException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBoth()
    {
        // Arrange
        const string message = "Custom message";
        var innerException = new InvalidOperationException("Inner");

        // Act
        var exception = new AggregateNotFoundException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Constructor_WithDetails_SetsAllProperties()
    {
        // Arrange
        var aggregateType = typeof(string);
        const string objectId = "123";
        const string objectType = "Order";

        // Act
        var exception = new AggregateNotFoundException(aggregateType, objectId, objectType);

        // Assert
        Assert.Equal(aggregateType, exception.AggregateType);
        Assert.Equal(objectId, exception.ObjectId);
        Assert.Equal(objectType, exception.ObjectType);
        Assert.Contains("String", exception.Message);
        Assert.Contains(objectId, exception.Message);
        Assert.Contains(objectType, exception.Message);
    }

    [Fact]
    public void Constructor_WithDetails_CreatesDescriptiveMessage()
    {
        // Arrange
        var aggregateType = typeof(int);
        const string objectId = "order-456";
        const string objectType = "MyAggregate";

        // Act
        var exception = new AggregateNotFoundException(aggregateType, objectId, objectType);

        // Assert
        Assert.Equal(
            "Aggregate of type 'Int32' with object ID 'order-456' (object type: 'MyAggregate') was not found.",
            exception.Message);
    }
}
