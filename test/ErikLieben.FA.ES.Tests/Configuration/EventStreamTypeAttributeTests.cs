using System;
using Xunit;
using ErikLieben.FA.ES.Configuration;

namespace ErikLieben.FA.ES.Tests.Configuration;

public class EventStreamTypeAttributeTests
{
    [Fact]
    public void Should_create_instance_with_same_value_for_all_properties_using_single_parameter_constructor()
    {
        // Arrange
        string commonValue = "blob";

        // Act
        var sut = new EventStreamTypeAttribute(commonValue);

        // Assert
        Assert.Equal(commonValue, sut.StreamType);
        Assert.Equal(commonValue, sut.DocumentType);
        Assert.Equal(commonValue, sut.DocumentTagType);
        Assert.Equal(commonValue, sut.EventStreamTagType);
        Assert.Equal(commonValue, sut.DocumentRefType);
    }

    [Fact]
    public void Should_create_instance_with_specific_values_for_each_property()
    {
        // Arrange
        string streamType = "blob";
        string documentType = "cosmos";
        string documentTagType = "redis";
        string eventStreamTagType = "memory";
        string documentRefType = "sql";

        // Act
        var sut = new EventStreamTypeAttribute(
            streamType: streamType,
            documentType: documentType,
            documentTagType: documentTagType,
            eventStreamTagType: eventStreamTagType,
            documentRefType: documentRefType);

        // Assert
        Assert.Equal(streamType, sut.StreamType);
        Assert.Equal(documentType, sut.DocumentType);
        Assert.Equal(documentTagType, sut.DocumentTagType);
        Assert.Equal(eventStreamTagType, sut.EventStreamTagType);
        Assert.Equal(documentRefType, sut.DocumentRefType);
    }

    [Fact]
    public void Should_create_instance_with_null_values_when_no_parameters_provided()
    {
        // Arrange & Act
        var sut = new EventStreamTypeAttribute();

        // Assert
        Assert.Null(sut.StreamType);
        Assert.Null(sut.DocumentType);
        Assert.Null(sut.DocumentTagType);
        Assert.Null(sut.EventStreamTagType);
        Assert.Null(sut.DocumentRefType);
    }

    [Fact]
    public void Should_create_instance_with_partial_values()
    {
        // Arrange
        string streamType = "blob";
        string documentType = "cosmos";

        // Act
        var sut = new EventStreamTypeAttribute(
            streamType: streamType,
            documentType: documentType);

        // Assert
        Assert.Equal(streamType, sut.StreamType);
        Assert.Equal(documentType, sut.DocumentType);
        Assert.Null(sut.DocumentTagType);
        Assert.Null(sut.EventStreamTagType);
        Assert.Null(sut.DocumentRefType);
    }

    [Fact]
    public void Should_have_correct_attribute_usage()
    {
        // Arrange
        var attributeType = typeof(EventStreamTypeAttribute);

        // Act
        var attributeUsageAttribute = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            attributeType, typeof(AttributeUsageAttribute));

        // Assert
        Assert.NotNull(attributeUsageAttribute);
        Assert.Equal(AttributeTargets.Class, attributeUsageAttribute.ValidOn);
        Assert.False(attributeUsageAttribute.AllowMultiple);
        Assert.False(attributeUsageAttribute.Inherited);
    }

    [Theory]
    [InlineData("blob")]
    [InlineData("cosmos")]
    [InlineData("memory")]
    [InlineData("")]
    public void Should_accept_various_string_values(string value)
    {
        // Arrange & Act
        var sut = new EventStreamTypeAttribute(value);

        // Assert
        Assert.Equal(value, sut.StreamType);
        Assert.Equal(value, sut.DocumentType);
        Assert.Equal(value, sut.DocumentTagType);
        Assert.Equal(value, sut.EventStreamTagType);
        Assert.Equal(value, sut.DocumentRefType);
    }

    [Fact]
    public void Should_be_sealed_class()
    {
        // Arrange
        var attributeType = typeof(EventStreamTypeAttribute);

        // Act & Assert
        Assert.True(attributeType.IsSealed);
    }

    [Fact]
    public void Should_inherit_from_attribute()
    {
        // Arrange
        var attributeType = typeof(EventStreamTypeAttribute);

        // Act & Assert
        Assert.True(typeof(Attribute).IsAssignableFrom(attributeType));
    }

    [Fact]
    public void Should_allow_setting_only_streamType()
    {
        // Arrange & Act
        var sut = new EventStreamTypeAttribute(streamType: "blob");

        // Assert
        Assert.Equal("blob", sut.StreamType);
        Assert.Null(sut.DocumentType);
        Assert.Null(sut.DocumentTagType);
        Assert.Null(sut.EventStreamTagType);
        Assert.Null(sut.DocumentRefType);
    }

    [Fact]
    public void Should_allow_mixed_null_and_non_null_values()
    {
        // Arrange & Act
        var sut = new EventStreamTypeAttribute(
            streamType: "blob",
            documentType: null,
            documentTagType: "cosmos",
            eventStreamTagType: null,
            documentRefType: "sql");

        // Assert
        Assert.Equal("blob", sut.StreamType);
        Assert.Null(sut.DocumentType);
        Assert.Equal("cosmos", sut.DocumentTagType);
        Assert.Null(sut.EventStreamTagType);
        Assert.Equal("sql", sut.DocumentRefType);
    }
}
