using System;
using Xunit;
using ErikLieben.FA.ES.Configuration;

namespace ErikLieben.FA.ES.Tests.Configuration;

public class EventStreamBlobSettingsAttributeTests
{
    [Fact]
    public void Should_create_instance_with_same_value_for_all_properties_using_single_parameter_constructor()
    {
        // Arrange
        string commonValue = "Store2";

        // Act
        var sut = new EventStreamBlobSettingsAttribute(commonValue);

        // Assert
        Assert.Equal(commonValue, sut.DataStore);
        Assert.Equal(commonValue, sut.DocumentStore);
        Assert.Equal(commonValue, sut.DocumentTagStore);
        Assert.Equal(commonValue, sut.StreamTagStore);
        Assert.Equal(commonValue, sut.SnapShotStore);
    }

    [Fact]
    public void Should_create_instance_with_specific_values_for_each_property()
    {
        // Arrange
        string dataStore = "HighThroughputStore";
        string documentStore = "StandardStore";
        string documentTagStore = "TagStore";
        string streamTagStore = "StreamTagStore";
        string snapShotStore = "ColdStore";

        // Act
        var sut = new EventStreamBlobSettingsAttribute(
            dataStore: dataStore,
            documentStore: documentStore,
            documentTagStore: documentTagStore,
            streamTagStore: streamTagStore,
            snapShotStore: snapShotStore);

        // Assert
        Assert.Equal(dataStore, sut.DataStore);
        Assert.Equal(documentStore, sut.DocumentStore);
        Assert.Equal(documentTagStore, sut.DocumentTagStore);
        Assert.Equal(streamTagStore, sut.StreamTagStore);
        Assert.Equal(snapShotStore, sut.SnapShotStore);
    }

    [Fact]
    public void Should_create_instance_with_null_values_when_no_parameters_provided()
    {
        // Arrange & Act
        var sut = new EventStreamBlobSettingsAttribute();

        // Assert
        Assert.Null(sut.DataStore);
        Assert.Null(sut.DocumentStore);
        Assert.Null(sut.DocumentTagStore);
        Assert.Null(sut.StreamTagStore);
        Assert.Null(sut.SnapShotStore);
    }

    [Fact]
    public void Should_create_instance_with_partial_values()
    {
        // Arrange
        string dataStore = "HighThroughputStore";
        string documentStore = "StandardStore";

        // Act
        var sut = new EventStreamBlobSettingsAttribute(
            dataStore: dataStore,
            documentStore: documentStore);

        // Assert
        Assert.Equal(dataStore, sut.DataStore);
        Assert.Equal(documentStore, sut.DocumentStore);
        Assert.Null(sut.DocumentTagStore);
        Assert.Null(sut.StreamTagStore);
        Assert.Null(sut.SnapShotStore);
    }

    [Fact]
    public void Should_have_correct_attribute_usage()
    {
        // Arrange
        var attributeType = typeof(EventStreamBlobSettingsAttribute);

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
    [InlineData("Store1")]
    [InlineData("Store2")]
    [InlineData("ComplianceStore")]
    [InlineData("")]
    public void Should_accept_various_string_values(string value)
    {
        // Arrange & Act
        var sut = new EventStreamBlobSettingsAttribute(value);

        // Assert
        Assert.Equal(value, sut.DataStore);
        Assert.Equal(value, sut.DocumentStore);
        Assert.Equal(value, sut.DocumentTagStore);
        Assert.Equal(value, sut.StreamTagStore);
        Assert.Equal(value, sut.SnapShotStore);
    }

    [Fact]
    public void Should_be_sealed_class()
    {
        // Arrange
        var attributeType = typeof(EventStreamBlobSettingsAttribute);

        // Act & Assert
        Assert.True(attributeType.IsSealed);
    }

    [Fact]
    public void Should_inherit_from_attribute()
    {
        // Arrange
        var attributeType = typeof(EventStreamBlobSettingsAttribute);

        // Act & Assert
        Assert.True(typeof(Attribute).IsAssignableFrom(attributeType));
    }

    [Fact]
    public void Should_allow_setting_only_dataStore()
    {
        // Arrange & Act
        var sut = new EventStreamBlobSettingsAttribute(dataStore: "Store2");

        // Assert
        Assert.Equal("Store2", sut.DataStore);
        Assert.Null(sut.DocumentStore);
        Assert.Null(sut.DocumentTagStore);
        Assert.Null(sut.StreamTagStore);
        Assert.Null(sut.SnapShotStore);
    }

    [Fact]
    public void Should_allow_mixed_null_and_non_null_values()
    {
        // Arrange & Act
        var sut = new EventStreamBlobSettingsAttribute(
            dataStore: "Store1",
            documentStore: null,
            documentTagStore: "Store2",
            streamTagStore: null,
            snapShotStore: "Store3");

        // Assert
        Assert.Equal("Store1", sut.DataStore);
        Assert.Null(sut.DocumentStore);
        Assert.Equal("Store2", sut.DocumentTagStore);
        Assert.Null(sut.StreamTagStore);
        Assert.Equal("Store3", sut.SnapShotStore);
    }

    [Fact]
    public void Should_support_compliance_store_scenario()
    {
        // Arrange
        string complianceStore = "ComplianceStore";

        // Act
        var sut = new EventStreamBlobSettingsAttribute(complianceStore);

        // Assert - All stores route to compliance store
        Assert.Equal(complianceStore, sut.DataStore);
        Assert.Equal(complianceStore, sut.DocumentStore);
        Assert.Equal(complianceStore, sut.DocumentTagStore);
        Assert.Equal(complianceStore, sut.StreamTagStore);
        Assert.Equal(complianceStore, sut.SnapShotStore);
    }

    [Fact]
    public void Should_support_mixed_performance_tiers_scenario()
    {
        // Arrange & Act - High performance for data, cold storage for snapshots
        var sut = new EventStreamBlobSettingsAttribute(
            dataStore: "HighThroughputStore",
            snapShotStore: "ColdStore");

        // Assert
        Assert.Equal("HighThroughputStore", sut.DataStore);
        Assert.Null(sut.DocumentStore);
        Assert.Null(sut.DocumentTagStore);
        Assert.Null(sut.StreamTagStore);
        Assert.Equal("ColdStore", sut.SnapShotStore);
    }
}
