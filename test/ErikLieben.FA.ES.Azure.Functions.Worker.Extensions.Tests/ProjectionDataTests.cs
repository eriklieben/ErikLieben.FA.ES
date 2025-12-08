using System.Text.Json;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class ProjectionDataTests
{
    public class DefaultConstructor : ProjectionDataTests
    {
        [Fact]
        public void Should_set_default_values()
        {
            // Arrange & Act
            var sut = new ProjectionData();

            // Assert
            Assert.Null(sut.BlobName);
            Assert.True(sut.CreateIfNotExists);
        }
    }

    public class ParameterizedConstructor : ProjectionDataTests
    {
        [Fact]
        public void Should_set_BlobName()
        {
            // Arrange
            var blobName = "my-blob";

            // Act
            var sut = new ProjectionData(blobName);

            // Assert
            Assert.Equal(blobName, sut.BlobName);
        }

        [Fact]
        public void Should_set_CreateIfNotExists()
        {
            // Arrange
            var createIfNotExists = false;

            // Act
            var sut = new ProjectionData(createIfNotExists: createIfNotExists);

            // Assert
            Assert.False(sut.CreateIfNotExists);
        }

        [Fact]
        public void Should_set_both_parameters()
        {
            // Arrange
            var blobName = "custom-blob";
            var createIfNotExists = false;

            // Act
            var sut = new ProjectionData(blobName, createIfNotExists);

            // Assert
            Assert.Equal(blobName, sut.BlobName);
            Assert.False(sut.CreateIfNotExists);
        }

        [Fact]
        public void Should_allow_null_BlobName()
        {
            // Arrange & Act
            var sut = new ProjectionData(null, true);

            // Assert
            Assert.Null(sut.BlobName);
            Assert.True(sut.CreateIfNotExists);
        }
    }

    public class Properties : ProjectionDataTests
    {
        [Fact]
        public void Should_allow_setting_BlobName()
        {
            // Arrange
            var sut = new ProjectionData();

            // Act
            sut.BlobName = "updated-blob";

            // Assert
            Assert.Equal("updated-blob", sut.BlobName);
        }

        [Fact]
        public void Should_allow_setting_CreateIfNotExists()
        {
            // Arrange
            var sut = new ProjectionData();

            // Act
            sut.CreateIfNotExists = false;

            // Assert
            Assert.False(sut.CreateIfNotExists);
        }
    }

    public class JsonSerialization : ProjectionDataTests
    {
        [Fact]
        public void Should_serialize_and_deserialize_correctly()
        {
            // Arrange
            var original = new ProjectionData("test-blob", false);

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<ProjectionData>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.BlobName, deserialized!.BlobName);
            Assert.Equal(original.CreateIfNotExists, deserialized.CreateIfNotExists);
        }

        [Fact]
        public void Should_deserialize_with_default_values()
        {
            // Arrange
            var json = "{}";

            // Act
            var deserialized = JsonSerializer.Deserialize<ProjectionData>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Null(deserialized!.BlobName);
            Assert.True(deserialized.CreateIfNotExists);
        }

        [Fact]
        public void Should_deserialize_with_partial_values()
        {
            // Arrange
            var json = """{"BlobName":"partial-blob"}""";

            // Act
            var deserialized = JsonSerializer.Deserialize<ProjectionData>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("partial-blob", deserialized!.BlobName);
            Assert.True(deserialized.CreateIfNotExists);
        }
    }
}
