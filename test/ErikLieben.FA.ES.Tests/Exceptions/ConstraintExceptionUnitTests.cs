using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class ConstraintExceptionTests
{
    [Fact]
    public void Should_set_message_and_constraint()
    {
        // Arrange
        var message = "Test constraint message";
        var constraint = new Constraint();

        // Act
        var sut = new ConstraintException(message, constraint);

        // Assert
        Assert.Equal("[ELFAES-BIZ-0001] " + message, sut.Message);
        Assert.Equal(constraint, sut.Constraint);
    }

    [Fact]
    public void Should_inherit_from_exception()
    {
        // Arrange
        var message = "Test constraint message";
        var constraint = new Constraint();

        // Act
        var sut = new ConstraintException(message, constraint);

        // Assert
        Assert.IsAssignableFrom<Exception>(sut);
    }

    [Fact]
    public void Should_have_serializable_attribute()
    {
        // Arrange & Act
        var type = typeof(ConstraintException);

        // Assert
        var serializableAttribute = type.GetCustomAttributes(typeof(SerializableAttribute), false);
        Assert.NotEmpty(serializableAttribute);
    }
}
