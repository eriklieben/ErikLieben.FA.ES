using Xunit;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

public class ProjectionAttributeTests
{
    [Fact]
    public void Default_constructor_should_set_default_values()
    {
        // Act
        var sut = new ProjectionAttribute();

        // Assert
        Assert.Null(sut.BlobName);
        Assert.True(sut.CreateIfNotExists);
        Assert.Null(sut.Connection);
    }

    [Fact]
    public void Constructor_with_blob_name_should_set_blob_name()
    {
        // Arrange
        var blobName = "test-projection";

        // Act
        var sut = new ProjectionAttribute(blobName);

        // Assert
        Assert.Equal(blobName, sut.BlobName);
        Assert.True(sut.CreateIfNotExists);
    }

    [Fact]
    public void Properties_should_be_settable()
    {
        // Arrange
        var sut = new ProjectionAttribute();

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
