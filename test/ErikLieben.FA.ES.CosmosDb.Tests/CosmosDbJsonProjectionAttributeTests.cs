using System;
using ErikLieben.FA.ES.CosmosDb;
using Xunit;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbJsonProjectionAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void Should_accept_container_parameter()
        {
            // Arrange
            string container = "test-container";

            // Act
            var sut = new CosmosDbJsonProjectionAttribute(container);

            // Assert
            Assert.NotNull(sut);
            Assert.Equal(container, sut.Container);
        }

        [Fact]
        public void Should_inherit_from_attribute()
        {
            // Arrange & Act
            var sut = new CosmosDbJsonProjectionAttribute("test-container");

            // Assert
            Assert.IsAssignableFrom<Attribute>(sut);
        }

        [Fact]
        public void Should_have_default_partition_key_path()
        {
            // Arrange & Act
            var sut = new CosmosDbJsonProjectionAttribute("test-container");

            // Assert
            Assert.Equal("/projectionName", sut.PartitionKeyPath);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_allow_setting_connection_property_during_initialization()
        {
            // Arrange
            string expectedConnection = "TestConnection";

            // Act
            var sut = new CosmosDbJsonProjectionAttribute("test-container")
            {
                Connection = expectedConnection
            };

            // Assert
            Assert.Equal(expectedConnection, sut.Connection);
        }

        [Fact]
        public void Should_have_nullable_connection_property()
        {
            // Arrange & Act
            var sut = new CosmosDbJsonProjectionAttribute("test-container");

            // Assert
            Assert.Null(sut.Connection);
        }

        [Fact]
        public void Should_allow_setting_partition_key_path_during_initialization()
        {
            // Arrange
            string expectedPartitionKeyPath = "/customKey";

            // Act
            var sut = new CosmosDbJsonProjectionAttribute("test-container")
            {
                PartitionKeyPath = expectedPartitionKeyPath
            };

            // Assert
            Assert.Equal(expectedPartitionKeyPath, sut.PartitionKeyPath);
        }
    }

    public class Usage
    {
        [Fact]
        public void Should_be_usable_as_attribute()
        {
            // Arrange & Act
            var type = typeof(TestClassWithAttribute);
            var attributes = type.GetCustomAttributes(typeof(CosmosDbJsonProjectionAttribute), false);

            // Assert
            Assert.Single(attributes);
            var attribute = (CosmosDbJsonProjectionAttribute)attributes[0];
            Assert.Equal("projections", attribute.Container);
            Assert.Equal("TestConnection", attribute.Connection);
        }

        [Fact]
        public void Should_be_usable_with_custom_partition_key()
        {
            // Arrange & Act
            var type = typeof(TestClassWithCustomPartitionKey);
            var attributes = type.GetCustomAttributes(typeof(CosmosDbJsonProjectionAttribute), false);

            // Assert
            Assert.Single(attributes);
            var attribute = (CosmosDbJsonProjectionAttribute)attributes[0];
            Assert.Equal("/customPartitionKey", attribute.PartitionKeyPath);
        }

        [CosmosDbJsonProjection("projections", Connection = "TestConnection")]
        private class TestClassWithAttribute
        {
        }

        [CosmosDbJsonProjection("projections", PartitionKeyPath = "/customPartitionKey")]
        private class TestClassWithCustomPartitionKey
        {
        }
    }

    public class AttributeUsage
    {
        [Fact]
        public void Should_not_allow_multiple_on_same_class()
        {
            // Arrange
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbJsonProjectionAttribute),
                typeof(AttributeUsageAttribute));

            // Assert
            Assert.NotNull(attributeUsage);
            Assert.False(attributeUsage.AllowMultiple);
        }

        [Fact]
        public void Should_only_be_applicable_to_classes()
        {
            // Arrange
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbJsonProjectionAttribute),
                typeof(AttributeUsageAttribute));

            // Assert
            Assert.NotNull(attributeUsage);
            Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
        }

        [Fact]
        public void Should_not_be_inherited()
        {
            // Arrange
            var attributeUsage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
                typeof(CosmosDbJsonProjectionAttribute),
                typeof(AttributeUsageAttribute));

            // Assert
            Assert.NotNull(attributeUsage);
            Assert.False(attributeUsage.Inherited);
        }
    }
}
