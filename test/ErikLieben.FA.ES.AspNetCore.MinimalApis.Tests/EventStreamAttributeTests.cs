namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests;

public class EventStreamAttributeTests
{
    [Fact]
    public void DefaultConstructor_SetsRouteParameterNameToId()
    {
        // Act
        var attribute = new EventStreamAttribute();

        // Assert
        Assert.Equal("id", attribute.RouteParameterName);
    }

    [Fact]
    public void Constructor_WithRouteParameterName_SetsRouteParameterName()
    {
        // Arrange
        const string routeParam = "orderId";

        // Act
        var attribute = new EventStreamAttribute(routeParam);

        // Assert
        Assert.Equal(routeParam, attribute.RouteParameterName);
    }

    [Fact]
    public void Constructor_WithNullRouteParameterName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EventStreamAttribute(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public void Constructor_WithEmptyOrWhitespaceRouteParameterName_ThrowsArgumentException(string routeParam)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new EventStreamAttribute(routeParam));
    }

    [Fact]
    public void CreateIfNotExists_DefaultsToFalse()
    {
        // Act
        var attribute = new EventStreamAttribute();

        // Assert
        Assert.False(attribute.CreateIfNotExists);
    }

    [Fact]
    public void CreateIfNotExists_CanBeSetToTrue()
    {
        // Act
        var attribute = new EventStreamAttribute { CreateIfNotExists = true };

        // Assert
        Assert.True(attribute.CreateIfNotExists);
    }

    [Fact]
    public void ObjectType_DefaultsToNull()
    {
        // Act
        var attribute = new EventStreamAttribute();

        // Assert
        Assert.Null(attribute.ObjectType);
    }

    [Fact]
    public void ObjectType_CanBeSet()
    {
        // Arrange
        const string objectType = "Order";

        // Act
        var attribute = new EventStreamAttribute { ObjectType = objectType };

        // Assert
        Assert.Equal(objectType, attribute.ObjectType);
    }

    [Fact]
    public void Store_DefaultsToNull()
    {
        // Act
        var attribute = new EventStreamAttribute();

        // Assert
        Assert.Null(attribute.Store);
    }

    [Fact]
    public void Store_CanBeSet()
    {
        // Arrange
        const string store = "blob";

        // Act
        var attribute = new EventStreamAttribute { Store = store };

        // Assert
        Assert.Equal(store, attribute.Store);
    }

    [Fact]
    public void Attribute_CanTargetParameters()
    {
        // Act
        var usage = typeof(EventStreamAttribute)
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
        var usage = typeof(EventStreamAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        Assert.Equal(AttributeTargets.Parameter, usage.ValidOn);
    }
}
