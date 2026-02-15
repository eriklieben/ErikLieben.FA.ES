

using System;
using System.Text.Json;
using ErikLieben.FA.ES.JsonConverters;
using Xunit;

namespace ErikLieben.FA.ES.Tests;

public class ActionMetadataTests
{
    [Fact]
    public void Should_return_an_empty_json_string_when_serializing_an_empty_ActionMetadata()
    {
        // Arrange
        var sut = new ActionMetadata();

        // Act
        var result = JsonSerializer.Serialize(sut);

        // Assert
        Assert.Equal("{}", result);
    }

    [Fact]
    public void
        Should_return_a_json_string_with_the_CorrelationId_when_serializing_an_ActionMetadata_with_a_CorrelationId()
    {
        // Arrange
        var sut = new ActionMetadata("CorrelationId");

        // Act
        var result = JsonSerializer.Serialize(sut);

        // Assert
        Assert.Equal("{\"CorrelationId\":\"CorrelationId\"}", result);
    }

    [Fact]
    public void Should_return_a_json_string_with_the_CausationId_when_serializing_an_ActionMetadata_with_a_CausationId()
    {
        // Arrange
        var sut = new ActionMetadata(CausationId: "CausationId");

        // Act
        var result = JsonSerializer.Serialize(sut);

        // Assert
        Assert.Equal("{\"CausationId\":\"CausationId\"}", result);
    }

    [Fact]
    public void
        Should_return_a_json_string_with_the_OriginatedFromUser_when_serializing_an_ActionMetadata_with_a_OriginatedFromUser()
    {
        // Arrange
        var userProfileVt = new VersionToken("userProfile", "123", "000", 324);
        var sut = new ActionMetadata(OriginatedFromUser: userProfileVt);
        var settings = new JsonSerializerOptions();
        settings.Converters.Add(new VersionTokenJsonConverter());

        // Act
        var result = JsonSerializer.Serialize(sut, settings);

        // Assert
        var vt = JsonSerializer.Serialize(userProfileVt, settings);
        Assert.Equal($"{{\"OriginatedFromUser\":{vt}}}", result);
    }

    [Fact]
    public void
        Should_return_a_json_string_with_the_EventOccuredAt_when_serializing_an_ActionMetadata_with_a_EventOccuredAt()
    {
        // Arrange
        var dateTime = DateTimeOffset.UtcNow;
        var sut = new ActionMetadata(EventOccuredAt: dateTime);

        // Act
        var result = JsonSerializer.Serialize(sut);

        // Assert
        Assert.Equal("{\"EventOccuredAt\":" + JsonSerializer.Serialize(dateTime) + "}", result);
    }

    [Fact]
    public void Should_return_a_json_string_with_the_IdempotentKey_when_serializing_an_ActionMetadata_with_an_IdempotentKey()
    {
        // Arrange
        var sut = new ActionMetadata(IdempotentKey: "order__123__stream__00000000000000000005#SendConfirmation");

        // Act
        var result = JsonSerializer.Serialize(sut);

        // Assert
        Assert.Equal("{\"IdempotentKey\":\"order__123__stream__00000000000000000005#SendConfirmation\"}", result);
    }

    [Fact]
    public void Should_omit_IdempotentKey_from_json_when_null()
    {
        // Arrange
        var sut = new ActionMetadata(CorrelationId: "test-correlation");

        // Act
        var result = JsonSerializer.Serialize(sut);

        // Assert
        Assert.DoesNotContain("IdempotentKey", result);
    }
}
