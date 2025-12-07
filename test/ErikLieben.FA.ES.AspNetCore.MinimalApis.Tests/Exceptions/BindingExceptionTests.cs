using ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests.Exceptions;

public class BindingExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultMessage()
    {
        // Act
        var exception = new BindingException();

        // Assert
        Assert.Equal("Parameter binding failed.", exception.Message);
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        // Arrange
        const string message = "Custom message";

        // Act
        var exception = new BindingException(message);

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
        var exception = new BindingException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void Constructor_WithParameterDetails_SetsAllProperties()
    {
        // Arrange
        const string parameterName = "order";
        var parameterType = typeof(string);
        const string reason = "Route parameter not found";

        // Act
        var exception = new BindingException(parameterName, parameterType, reason);

        // Assert
        Assert.Equal(parameterName, exception.ParameterName);
        Assert.Equal(parameterType, exception.ParameterType);
        Assert.Contains(parameterName, exception.Message);
        Assert.Contains("String", exception.Message);
        Assert.Contains(reason, exception.Message);
    }

    [Fact]
    public void Constructor_WithParameterDetailsAndInnerException_SetsAll()
    {
        // Arrange
        const string parameterName = "projection";
        var parameterType = typeof(int);
        const string reason = "Factory not found";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new BindingException(parameterName, parameterType, reason, innerException);

        // Assert
        Assert.Equal(parameterName, exception.ParameterName);
        Assert.Equal(parameterType, exception.ParameterType);
        Assert.Same(innerException, exception.InnerException);
        Assert.Contains(parameterName, exception.Message);
        Assert.Contains(reason, exception.Message);
    }

    [Fact]
    public void Constructor_WithParameterDetails_CreatesDescriptiveMessage()
    {
        // Arrange
        const string parameterName = "aggregate";
        var parameterType = typeof(double);
        const string reason = "Invalid object ID";

        // Act
        var exception = new BindingException(parameterName, parameterType, reason);

        // Assert
        Assert.Equal(
            "Failed to bind parameter 'aggregate' of type 'Double': Invalid object ID",
            exception.Message);
    }
}
