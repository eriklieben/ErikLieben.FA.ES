using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class SnapshotJsonTypeInfoNotSetExceptionTests
{
    [Fact]
    public void Should_set_correct_error_message()
    {
        // Arrange & Act
        var sut = new SnapshotJsonTypeInfoNotSetException();

        // Assert
        Assert.Equal("[ELFAES-CFG-0002] Snapshot JsonInfo type should be set to deserialize the snapshot type", sut.Message);
    }

    [Fact]
    public void Should_inherit_from_exception()
    {
        // Arrange & Act
        var sut = new SnapshotJsonTypeInfoNotSetException();

        // Assert
        Assert.IsType<Exception>(sut, exactMatch: false);
    }
}
