#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using ErikLieben.FA.ES.AspNetCore.MinimalApis.Binding;
using Microsoft.AspNetCore.Routing;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests.Binding;

public class RouteValueResolverTests
{
    [Fact]
    public void GetRouteValue_WhenParameterExists_ReturnsValue()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", "123" } };

        // Act
        var result = RouteValueResolver.GetRouteValue(routeValues, "id");

        // Assert
        Assert.Equal("123", result);
    }

    [Fact]
    public void GetRouteValue_WhenParameterDoesNotExist_ReturnsNull()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "other", "value" } };

        // Act
        var result = RouteValueResolver.GetRouteValue(routeValues, "id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetRouteValue_WhenValueIsNull_ReturnsNull()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", null } };

        // Act
        var result = RouteValueResolver.GetRouteValue(routeValues, "id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetRouteValue_WhenValueIsIntegral_ReturnsStringRepresentation()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", 42 } };

        // Act
        var result = RouteValueResolver.GetRouteValue(routeValues, "id");

        // Assert
        Assert.Equal("42", result);
    }

    [Fact]
    public void SubstituteRouteValues_WhenPatternIsNull_ReturnsNull()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", "123" } };

        // Act
        var result = RouteValueResolver.SubstituteRouteValues(null, routeValues);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SubstituteRouteValues_WhenPatternIsEmpty_ReturnsEmpty()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", "123" } };

        // Act
        var result = RouteValueResolver.SubstituteRouteValues(string.Empty, routeValues);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SubstituteRouteValues_WithSinglePlaceholder_SubstitutesValue()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", "123" } };

        // Act
        var result = RouteValueResolver.SubstituteRouteValues("{id}", routeValues);

        // Assert
        Assert.Equal("123", result);
    }

    [Fact]
    public void SubstituteRouteValues_WithMultiplePlaceholders_SubstitutesAllValues()
    {
        // Arrange
        var routeValues = new RouteValueDictionary
        {
            { "tenantId", "tenant1" },
            { "orderId", "order123" }
        };

        // Act
        var result = RouteValueResolver.SubstituteRouteValues("{tenantId}/orders/{orderId}", routeValues);

        // Assert
        Assert.Equal("tenant1/orders/order123", result);
    }

    [Fact]
    public void SubstituteRouteValues_WithMissingPlaceholder_LeavesPlaceholderUnchanged()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", "123" } };

        // Act
        var result = RouteValueResolver.SubstituteRouteValues("{tenantId}/items/{id}", routeValues);

        // Assert
        Assert.Equal("{tenantId}/items/123", result);
    }

    [Fact]
    public void SubstituteRouteValues_IsCaseInsensitive()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "ID", "123" } };

        // Act
        var result = RouteValueResolver.SubstituteRouteValues("{id}", routeValues);

        // Assert
        Assert.Equal("123", result);
    }

    [Fact]
    public void SubstituteRouteValues_WithNullRouteValue_SubstitutesEmptyString()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", null } };

        // Act
        var result = RouteValueResolver.SubstituteRouteValues("{id}", routeValues);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SubstituteRouteValues_WithNoPlaceholders_ReturnsOriginalPattern()
    {
        // Arrange
        var routeValues = new RouteValueDictionary { { "id", "123" } };

        // Act
        var result = RouteValueResolver.SubstituteRouteValues("static-name.json", routeValues);

        // Assert
        Assert.Equal("static-name.json", result);
    }
}
