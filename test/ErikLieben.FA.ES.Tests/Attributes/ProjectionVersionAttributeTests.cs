using System;
using System.Linq;
using ErikLieben.FA.ES.Attributes;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Attributes;

public class ProjectionVersionAttributeTests
{
    [Fact]
    public void Should_create_instance_with_version()
    {
        // Arrange & Act
        var attribute = new ProjectionVersionAttribute(2);

        // Assert
        Assert.Equal(2, attribute.Version);
    }

    [Fact]
    public void Should_have_default_version_constant_of_1()
    {
        // Assert
        Assert.Equal(1, ProjectionVersionAttribute.DefaultVersion);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Should_accept_valid_version_numbers(int version)
    {
        // Arrange & Act
        var attribute = new ProjectionVersionAttribute(version);

        // Assert
        Assert.Equal(version, attribute.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Should_throw_ArgumentOutOfRangeException_for_invalid_version(int version)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectionVersionAttribute(version));
        Assert.Equal("version", exception.ParamName);
        Assert.Contains("Schema version must be at least 1", exception.Message);
    }

    [Fact]
    public void Should_be_applicable_to_class_only()
    {
        // Arrange
        var attributeUsage = typeof(ProjectionVersionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
    }

    [Fact]
    public void Should_allow_version_property_to_be_set_via_init()
    {
        // Arrange & Act
        var attribute = new ProjectionVersionAttribute(1) { Version = 5 };

        // Assert
        Assert.Equal(5, attribute.Version);
    }
}
