using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToFindDocumentTagFactoryExceptionTests
{
    [Fact]
    public void Should_set_correct_error_message()
    {
        // Arrange
        var message = "Test document tag factory message";

        // Act
        var sut = new UnableToFindDocumentTagFactoryException(message);

        // Assert
        Assert.Equal("[ELFAES-CFG-0005] " + message, sut.Message);
    }

    [Fact]
    public void Should_inherit_from_exception()
    {
        // Arrange
        var message = "Test document tag factory message";

        // Act
        var sut = new UnableToFindDocumentTagFactoryException(message);

        // Assert
        Assert.IsType<Exception>(sut, exactMatch: false);
    }
}
