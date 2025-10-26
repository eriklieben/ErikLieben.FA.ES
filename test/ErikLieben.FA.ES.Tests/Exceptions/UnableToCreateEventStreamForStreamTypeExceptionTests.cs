using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class UnableToCreateEventStreamForStreamTypeExceptionTests
{
    [Fact]
    public void Should_set_correct_error_message()
    {
        // Arrange
        string streamType = "TestStream";
        string fallbackStreamType = "FallbackStream";

        // Act
        var sut = new UnableToCreateEventStreamForStreamTypeException(streamType, fallbackStreamType);

        // Assert
        Assert.Equal($"[ELFAES-CFG-0003] Unable to create EventStream of the type {streamType} or {fallbackStreamType}. Is your configuration correct?", sut.Message);
    }

    [Fact]
    public void Should_inherit_from_exception()
    {
        // Arrange
        string streamType = "TestStream";
        string fallbackStreamType = "FallbackStream";

        // Act
        var sut = new UnableToCreateEventStreamForStreamTypeException(streamType, fallbackStreamType);

        // Assert
        Assert.IsType<Exception>(sut, exactMatch: false);
    }
}
