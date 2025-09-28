using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class ConstraintExceptionSerializationTests
{
    public class Constructor
    {
        [Fact]
        public void Should_set_message_and_constraint()
        {
            // Arrange
            var constraint = Constraint.Existing;

            // Act
            var sut = new ConstraintException("constraint failed", constraint);

            // Assert
            Assert.Equal(constraint, sut.Constraint);
            Assert.Equal("[ELFAES-BIZ-0001] constraint failed", sut.Message);
        }
    }
}
