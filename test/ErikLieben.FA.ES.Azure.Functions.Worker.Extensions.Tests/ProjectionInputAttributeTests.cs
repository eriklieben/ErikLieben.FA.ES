using System;
using System.Reflection;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class ProjectionInputAttributeTests
{
    public class DefaultConstructor : ProjectionInputAttributeTests
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange & Act
            var sut = new ProjectionInputAttribute();

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_set_default_values()
        {
            // Arrange & Act
            var sut = new ProjectionInputAttribute();

            // Assert
            Assert.Null(sut.BlobName);
            Assert.True(sut.CreateIfNotExists);
        }
    }

    public class BlobNameConstructor : ProjectionInputAttributeTests
    {
        [Fact]
        public void Should_set_BlobName()
        {
            // Arrange
            var blobName = "my-projection-blob";

            // Act
            var sut = new ProjectionInputAttribute(blobName);

            // Assert
            Assert.Equal(blobName, sut.BlobName);
        }

        [Fact]
        public void Should_set_default_CreateIfNotExists()
        {
            // Arrange
            var blobName = "my-projection-blob";

            // Act
            var sut = new ProjectionInputAttribute(blobName);

            // Assert
            Assert.True(sut.CreateIfNotExists);
        }
    }

    public class Properties : ProjectionInputAttributeTests
    {
        [Fact]
        public void Should_allow_setting_BlobName()
        {
            // Arrange
            var sut = new ProjectionInputAttribute();

            // Act
            sut.BlobName = "updated-blob-name";

            // Assert
            Assert.Equal("updated-blob-name", sut.BlobName);
        }

        [Fact]
        public void Should_allow_setting_CreateIfNotExists_to_false()
        {
            // Arrange
            var sut = new ProjectionInputAttribute();

            // Act
            sut.CreateIfNotExists = false;

            // Assert
            Assert.False(sut.CreateIfNotExists);
        }
    }

    public class AttributeMetadata : ProjectionInputAttributeTests
    {
        [Fact]
        public void Should_have_AttributeUsage_targeting_Parameter()
        {
            // Arrange
            var attributeType = typeof(ProjectionInputAttribute);

            // Act
            var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

            // Assert
            Assert.NotNull(usage);
            Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Parameter));
        }

        [Fact]
        public void Should_have_InputConverter_attribute_with_ProjectionConverter()
        {
            // Arrange
            var attributeType = typeof(ProjectionInputAttribute);

            // Act
            var inputConverter = attributeType.GetCustomAttribute<InputConverterAttribute>();

            // Assert
            Assert.NotNull(inputConverter);
            Assert.Equal(typeof(ProjectionConverter), inputConverter!.ConverterType);
        }

        [Fact]
        public void Should_have_ConverterFallbackBehavior_attribute()
        {
            // Arrange
            var attributeType = typeof(ProjectionInputAttribute);

            // Act
            var fallbackBehavior = attributeType.GetCustomAttribute<ConverterFallbackBehaviorAttribute>();

            // Assert
            Assert.NotNull(fallbackBehavior);
            Assert.Equal(ConverterFallbackBehavior.Default, fallbackBehavior!.Behavior);
        }

        [Fact]
        public void Should_inherit_from_InputBindingAttribute()
        {
            // Arrange & Act
            var sut = new ProjectionInputAttribute();

            // Assert
            Assert.IsAssignableFrom<InputBindingAttribute>(sut);
        }
    }
}
