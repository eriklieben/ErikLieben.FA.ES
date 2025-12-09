using System;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToDeserializeInTransitEventExceptionTests
{
    [Fact]
    public void Should_set_correct_error_message()
    {
        // Arrange & Act
        var sut = new UnableToDeserializeInTransitEventException();

        // Assert
        Assert.Equal("[ELFAES-VAL-0001] Unable to deserialize to event, value is 'null'", sut.Message);
    }

    [Fact]
    public void Should_inherit_from_exception()
    {
        // Arrange & Act
        var sut = new UnableToDeserializeInTransitEventException();

        // Assert
        Assert.IsType<Exception>(sut, exactMatch: false);
    }
}
