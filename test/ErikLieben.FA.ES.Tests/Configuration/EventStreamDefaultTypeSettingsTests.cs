using System;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Configuration;

public class EventStreamDefaultTypeSettingsTests
{
    [Fact]
    public void Should_create_instance_with_specific_values_for_each_property()
    {
        // Arrange
        string streamType = "streamType";
        string documentType = "documentType";
        string documentTagType = "documentTagType";
        string eventStreamTagType = "eventStreamTagType";
        string documentRefType = "documentRefType";

        // Act
        var sut = new ErikLieben.FA.ES.Configuration.EventStreamDefaultTypeSettings(
            streamType,
            documentType,
            documentTagType,
            eventStreamTagType,
            documentRefType);

        // Assert
        Assert.Equal(streamType, sut.StreamType);
        Assert.Equal(documentType, sut.DocumentType);
        Assert.Equal(documentTagType, sut.DocumentTagType);
        Assert.Equal(eventStreamTagType, sut.EventStreamTagType);
        Assert.Equal(documentRefType, sut.DocumentRefType);
    }

    [Fact]
    public void Should_create_instance_with_same_value_for_all_properties()
    {
        // Arrange
        string commonValue = "commonType";

        // Act
        var sut = new ErikLieben.FA.ES.Configuration.EventStreamDefaultTypeSettings(commonValue);

        // Assert
        Assert.Equal(commonValue, sut.StreamType);
        Assert.Equal(commonValue, sut.DocumentType);
        Assert.Equal(commonValue, sut.DocumentTagType);
        Assert.Equal(commonValue, sut.EventStreamTagType);
        Assert.Equal(commonValue, sut.DocumentRefType);
    }

    [Fact]
    public void Should_create_instance_with_empty_strings_for_all_properties()
    {
        // Arrange & Act
        var sut = new ErikLieben.FA.ES.Configuration.EventStreamDefaultTypeSettings();

        // Assert
        Assert.Equal(string.Empty, sut.StreamType);
        Assert.Equal(string.Empty, sut.DocumentType);
        Assert.Equal(string.Empty, sut.DocumentTagType);
        Assert.Equal(string.Empty, sut.EventStreamTagType);
        Assert.Equal(string.Empty, sut.DocumentRefType);
    }

    [Fact]
    public void Should_have_value_equality_semantics()
    {
        // Arrange
        var settings1 = new ErikLieben.FA.ES.Configuration.EventStreamDefaultTypeSettings("test");
        var settings2 = new ErikLieben.FA.ES.Configuration.EventStreamDefaultTypeSettings("test");
        var settings3 = new ErikLieben.FA.ES.Configuration.EventStreamDefaultTypeSettings("different");

        // Act & Assert
        Assert.Equal(settings1, settings2);
        Assert.NotEqual(settings1, settings3);
        Assert.True(settings1.Equals(settings2));
        Assert.False(settings1.Equals(settings3));
        Assert.Equal(settings1.GetHashCode(), settings2.GetHashCode());
        Assert.NotEqual(settings1.GetHashCode(), settings3.GetHashCode());
    }
}
