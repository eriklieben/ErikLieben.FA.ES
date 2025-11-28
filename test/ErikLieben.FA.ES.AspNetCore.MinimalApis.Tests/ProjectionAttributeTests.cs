namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests;

public class ProjectionAttributeTests
{
    [Fact]
    public void DefaultConstructor_SetsBlobNamePatternToNull()
    {
        // Act
        var attribute = new ProjectionAttribute();

        // Assert
        Assert.Null(attribute.BlobNamePattern);
    }

    [Fact]
    public void Constructor_WithBlobNamePattern_SetsBlobNamePattern()
    {
        // Arrange
        const string pattern = "{id}";

        // Act
        var attribute = new ProjectionAttribute(pattern);

        // Assert
        Assert.Equal(pattern, attribute.BlobNamePattern);
    }

    [Fact]
    public void CreateIfNotExists_DefaultsToTrue()
    {
        // Act
        var attribute = new ProjectionAttribute();

        // Assert
        Assert.True(attribute.CreateIfNotExists);
    }

    [Fact]
    public void CreateIfNotExists_CanBeSetToFalse()
    {
        // Act
        var attribute = new ProjectionAttribute { CreateIfNotExists = false };

        // Assert
        Assert.False(attribute.CreateIfNotExists);
    }

    [Fact]
    public void Attribute_CanTargetParameters()
    {
        // Act
        var usage = typeof(ProjectionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Parameter));
    }

    [Fact]
    public void Attribute_CannotTargetOtherMembers()
    {
        // Act
        var usage = typeof(ProjectionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.Equal(AttributeTargets.Parameter, usage.ValidOn);
    }

    [Fact]
    public void Constructor_WithComplexPattern_PreservesPattern()
    {
        // Arrange
        const string pattern = "{tenantId}/orders/{orderId}";

        // Act
        var attribute = new ProjectionAttribute(pattern);

        // Assert
        Assert.Equal(pattern, attribute.BlobNamePattern);
    }
}
