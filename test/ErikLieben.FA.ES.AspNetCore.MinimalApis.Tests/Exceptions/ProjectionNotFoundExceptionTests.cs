#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests.Exceptions;

public class ProjectionNotFoundExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultMessage()
    {
        // Act
        var exception = new ProjectionNotFoundException();

        // Assert
        Assert.Equal("The requested projection was not found.", exception.Message);
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        // Arrange
        const string message = "Custom message";

        // Act
        var exception = new ProjectionNotFoundException(message);

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
        var exception = new ProjectionNotFoundException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Constructor_WithTypeAndBlobName_SetsAllProperties()
    {
        // Arrange
        var projectionType = typeof(string);
        const string blobName = "projection-123.json";

        // Act
        var exception = new ProjectionNotFoundException(projectionType, blobName);

        // Assert
        Assert.Equal(projectionType, exception.ProjectionType);
        Assert.Equal(blobName, exception.BlobName);
        Assert.Contains("String", exception.Message);
        Assert.Contains(blobName, exception.Message);
    }

    [Fact]
    public void Constructor_WithTypeAndNullBlobName_SetsDefaultBlobNameMessage()
    {
        // Arrange
        var projectionType = typeof(int);

        // Act
        var exception = new ProjectionNotFoundException(projectionType, null);

        // Assert
        Assert.Equal(projectionType, exception.ProjectionType);
        Assert.Null(exception.BlobName);
        Assert.Contains("default blob name", exception.Message);
    }

    [Fact]
    public void Constructor_WithDetails_CreatesDescriptiveMessageWithBlobName()
    {
        // Arrange
        var projectionType = typeof(double);
        const string blobName = "my-projection.json";

        // Act
        var exception = new ProjectionNotFoundException(projectionType, blobName);

        // Assert
        Assert.Equal(
            "Projection of type 'Double' with blob name 'my-projection.json' was not found.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WithDetails_CreatesDescriptiveMessageWithDefaultBlobName()
    {
        // Arrange
        var projectionType = typeof(bool);

        // Act
        var exception = new ProjectionNotFoundException(projectionType, null);

        // Assert
        Assert.Equal(
            "Projection of type 'Boolean' with default blob name was not found.",
            exception.Message);
    }
}
