#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using ErikLieben.FA.ES.AzureStorage.Blob;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob
{
    public class BlobJsonProjectionAttributeTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_accept_path_parameter()
            {
                // Arrange
                string path = "test/path";

                // Act
                var sut = new BlobJsonProjectionAttribute(path);

                // Assert
                Assert.NotNull(sut);
            }

            [Fact]
            public void Should_inherit_from_attribute()
            {
                // Arrange & Act
                var sut = new BlobJsonProjectionAttribute("test/path");

                // Assert
                Assert.IsType<Attribute>(sut, exactMatch: false);
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
                var sut = new BlobJsonProjectionAttribute("test/path")
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
                var sut = new BlobJsonProjectionAttribute("test/path");

                // Assert
                Assert.Null(sut.Connection);
            }
        }

        public class Usage
        {
            [Fact]
            public void Should_be_usable_as_attribute()
            {
                // Arrange & Act & Assert
                var type = typeof(TestClassWithAttribute);
                var attributes = type.GetCustomAttributes(typeof(BlobJsonProjectionAttribute), false);

                // Assert
                Assert.Single(attributes);
                var attribute = (BlobJsonProjectionAttribute)attributes[0];
                Assert.Equal("TestConnection", attribute.Connection);
            }

            [BlobJsonProjection("test/path", Connection = "TestConnection")]
            private class TestClassWithAttribute
            {
            }
        }
    }

}
