using Xunit;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

public class ProjectionAttributeDataTests
{
    [Fact]
    public void Default_constructor_should_set_default_values()
    {
        // Act
        var sut = new ProjectionAttributeData();

        // Assert
        Assert.Null(sut.BlobName);
        Assert.True(sut.CreateIfNotExists);
        Assert.Null(sut.Connection);
    }

    [Fact]
    public void Properties_should_be_settable()
    {
        // Arrange
        var sut = new ProjectionAttributeData();

        // Act
        sut.BlobName = "custom-blob";
        sut.CreateIfNotExists = false;
        sut.Connection = "MyStorageConnection";

        // Assert
        Assert.Equal("custom-blob", sut.BlobName);
        Assert.False(sut.CreateIfNotExists);
        Assert.Equal("MyStorageConnection", sut.Connection);
    }
}
