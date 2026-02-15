#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using ErikLieben.FA.ES.AzureStorage.Table;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Table;

public class TableProjectionAttributeTests
{
    public class Constructor
    {
        [Fact]
        public void Should_accept_table_name_parameter()
        {
            // Arrange
            string tableName = "testTable";

            // Act
            var sut = new TableProjectionAttribute(tableName);

            // Assert
            Assert.NotNull(sut);
            Assert.Equal(tableName, sut.TableName);
        }

        [Fact]
        public void Should_inherit_from_attribute()
        {
            // Arrange & Act
            var sut = new TableProjectionAttribute("testTable");

            // Assert
            Assert.IsType<Attribute>(sut, exactMatch: false);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_allow_setting_connection_name_property_during_initialization()
        {
            // Arrange
            string expectedConnection = "TestConnection";

            // Act
            var sut = new TableProjectionAttribute("testTable")
            {
                ConnectionName = expectedConnection
            };

            // Assert
            Assert.Equal(expectedConnection, sut.ConnectionName);
        }

        [Fact]
        public void Should_have_nullable_connection_name_property()
        {
            // Arrange & Act
            var sut = new TableProjectionAttribute("testTable");

            // Assert
            Assert.Null(sut.ConnectionName);
        }

        [Fact]
        public void Should_have_auto_create_table_default_to_true()
        {
            // Arrange & Act
            var sut = new TableProjectionAttribute("testTable");

            // Assert
            Assert.True(sut.AutoCreateTable);
        }

        [Fact]
        public void Should_allow_setting_auto_create_table_to_false()
        {
            // Arrange & Act
            var sut = new TableProjectionAttribute("testTable")
            {
                AutoCreateTable = false
            };

            // Assert
            Assert.False(sut.AutoCreateTable);
        }
    }

    public class Usage
    {
        [Fact]
        public void Should_be_usable_as_attribute()
        {
            // Arrange & Act & Assert
            var type = typeof(TestClassWithAttribute);
            var attributes = type.GetCustomAttributes(typeof(TableProjectionAttribute), false);

            // Assert
            Assert.Single(attributes);
            var attribute = (TableProjectionAttribute)attributes[0];
            Assert.Equal("TestConnection", attribute.ConnectionName);
            Assert.Equal("testTable", attribute.TableName);
        }

        [TableProjection("testTable", ConnectionName = "TestConnection")]
        private class TestClassWithAttribute
        {
        }
    }
}
