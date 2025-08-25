using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToFindDocumentFactoryExceptionTests
{
    [Fact]
    public void Should_set_correct_error_message()
    {
        // Arrange
        var message = "Test document factory message";
        
        // Act
        var sut = new UnableToFindDocumentFactoryException(message);

        // Assert
        Assert.Equal(message, sut.Message);
    }

    [Fact]
    public void Should_inherit_from_exception()
    {
        // Arrange
        var message = "Test document factory message";
        
        // Act
        var sut = new UnableToFindDocumentFactoryException(message);

        // Assert
        Assert.IsAssignableFrom<Exception>(sut);
    }
}