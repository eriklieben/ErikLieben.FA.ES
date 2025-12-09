using System;
using ErikLieben.FA.ES.VersionTokenParts;
using Xunit;

namespace ErikLieben.FA.ES.Tests.VersionTokenParts
{
    public class ObjectIdentifierTests
    {
        public class Ctor
        {
            [Fact]
            public void Should_create_instance_with_default_constructor()
            {
                // Arrange & Act
                var sut = new ObjectIdentifier();

                // Assert
                Assert.Equal(string.Empty, sut.ObjectName);
                Assert.Equal(string.Empty, sut.ObjectId);
                Assert.Equal("v1", sut.SchemaVersion);
                Assert.Equal("__", sut.Value);
            }

            [Fact]
            public void Should_create_instance_with_object_name_and_id()
            {
                // Arrange
                string objectName = "TestObject";
                string objectId = "123";

                // Act
                var sut = new ObjectIdentifier(objectName, objectId);

                // Assert
                Assert.Equal(objectName, sut.ObjectName);
                Assert.Equal(objectId, sut.ObjectId);
                Assert.Equal("v1", sut.SchemaVersion);
                Assert.Equal("TestObject__123", sut.Value);
            }

            [Fact]
            public void Should_create_instance_from_identifier_string()
            {
                // Arrange
                string identifierString = "TestObject__123";

                // Act
                var sut = new ObjectIdentifier(identifierString);

                // Assert
                Assert.Equal("TestObject", sut.ObjectName);
                Assert.Equal("123", sut.ObjectId);
                Assert.Equal("v1", sut.SchemaVersion);
                Assert.Equal(identifierString, sut.Value);
            }

            [Fact]
            public void Should_throw_when_identifier_string_is_null()
            {
                // Arrange
                string? identifierString = null;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new ObjectIdentifier(identifierString!));
            }

            [Theory]
            [InlineData("TestObject")]
            [InlineData("TestObject__")]
            [InlineData("TestObject__123__extra")]
            [InlineData("__")]
            public void Should_throw_when_identifier_string_format_is_invalid(string invalidIdentifier)
            {
                // Arrange, Act & Assert
                var exception = Assert.Throws<ArgumentException>(() => new ObjectIdentifier(invalidIdentifier));
                Assert.Contains("IdentifierString must consist out if 2 parts split by __", exception.Message);
                Assert.Contains(invalidIdentifier, exception.Message);
            }

            [Fact]
            public void Should_throw_when_object_name_is_null()
            {
                // Arrange
                string? objectName = null;
                string objectId = "123";

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new ObjectIdentifier(objectName!, objectId));
            }

            [Fact]
            public void Should_throw_when_object_id_is_null()
            {
                // Arrange
                string objectName = "TestObject";
                string? objectId = null;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new ObjectIdentifier(objectName, objectId!));
            }
        }

        public class ValuePropertyTests
        {
            [Fact]
            public void Should_return_combined_name_and_id()
            {
                // Arrange
                var sut = new ObjectIdentifier("TestObject", "123");

                // Act
                string value = sut.Value;

                // Assert
                Assert.Equal("TestObject__123", value);
            }

            [Fact]
            public void Should_return_empty_values_when_properties_are_empty()
            {
                // Arrange
                var sut = new ObjectIdentifier();

                // Act
                string value = sut.Value;

                // Assert
                Assert.Equal("__", value);
            }
        }

        public class ToStringTests
        {
            [Fact]
            public void Should_return_same_value_as_value_property()
            {
                // Arrange
                var sut = new ObjectIdentifier("TestObject", "123");

                // Act
                string result = sut.ToString();

                // Assert
                Assert.Equal(sut.Value, result);
                Assert.Equal("TestObject__123", result);
            }
        }

        public class CompareToObjectIdentifierTests
        {
            [Fact]
            public void Should_return_zero_for_equal_identifiers()
            {
                // Arrange
                var sut = new ObjectIdentifier("TestObject", "123");
                var other = new ObjectIdentifier("TestObject", "123");

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.Equal(0, result);
            }

            [Fact]
            public void Should_return_negative_value_when_less_than_other()
            {
                // Arrange
                var sut = new ObjectIdentifier("A", "123");
                var other = new ObjectIdentifier("B", "123");

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.True(result < 0);
            }

            [Fact]
            public void Should_return_positive_value_when_greater_than_other()
            {
                // Arrange
                var sut = new ObjectIdentifier("B", "123");
                var other = new ObjectIdentifier("A", "123");

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.True(result > 0);
            }

            [Fact]
            public void Should_handle_null_comparison()
            {
                // Arrange
                var sut = new ObjectIdentifier("TestObject", "123");
                ObjectIdentifier? other = null;

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.True(result > 0);
            }
        }

        public class CompareToObjectTests
        {
            [Fact]
            public void Should_return_zero_for_equal_identifiers()
            {
                // Arrange
                var sut = new ObjectIdentifier("TestObject", "123");
                object other = new ObjectIdentifier("TestObject", "123");

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.Equal(0, result);
            }

            [Fact]
            public void Should_return_negative_value_when_less_than_other()
            {
                // Arrange
                var sut = new ObjectIdentifier("A", "123");
                object other = new ObjectIdentifier("B", "123");

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.True(result < 0);
            }

            [Fact]
            public void Should_return_positive_value_when_greater_than_other()
            {
                // Arrange
                var sut = new ObjectIdentifier("B", "123");
                object other = new ObjectIdentifier("A", "123");

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.True(result > 0);
            }

            [Fact]
            public void Should_handle_null_comparison()
            {
                // Arrange
                var sut = new ObjectIdentifier("TestObject", "123");
                object? other = null;

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.True(result > 0);
            }

            [Fact]
            public void Should_handle_non_objectidentifier_comparison()
            {
                // Arrange
                var sut = new ObjectIdentifier("TestObject", "123");
                object other = "Some string";

                // Act
                int result = sut.CompareTo(other);

                // Assert
                Assert.True(result > 0);
            }
        }

        public class SchemaVersionTests
        {
            [Fact]
            public void Should_have_default_schema_version()
            {
                // Arrange & Act
                var sut = new ObjectIdentifier();

                // Assert
                Assert.Equal("v1", sut.SchemaVersion);
            }

            [Fact]
            public void Should_set_custom_schema_version()
            {
                // Arrange & Act
                var sut = new ObjectIdentifier
                {
                    SchemaVersion = "v2"
                };

                // Assert
                Assert.Equal("v2", sut.SchemaVersion);
            }

            [Fact]
            public void Should_maintain_schema_version_from_constructor()
            {
                // Arrange & Act
                var sut = new ObjectIdentifier("TestObject", "123")
                {
                    SchemaVersion = "v2"
                };

                // Assert
                Assert.Equal("v2", sut.SchemaVersion);
                Assert.Equal("TestObject", sut.ObjectName);
                Assert.Equal("123", sut.ObjectId);
            }
        }

        public class RecordEqualityTests
        {
            [Fact]
            public void Should_be_equal_when_all_properties_match()
            {
                // Arrange
                var sut1 = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "v2" };
                var sut2 = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "v2" };

                // Act & Assert
                Assert.Equal(sut1, sut2);
            }

            [Fact]
            public void Should_not_be_equal_when_object_name_differs()
            {
                // Arrange
                var sut1 = new ObjectIdentifier("TestObject1", "123");
                var sut2 = new ObjectIdentifier("TestObject2", "123");

                // Act & Assert
                Assert.NotEqual(sut1, sut2);
            }

            [Fact]
            public void Should_not_be_equal_when_object_id_differs()
            {
                // Arrange
                var sut1 = new ObjectIdentifier("TestObject", "123");
                var sut2 = new ObjectIdentifier("TestObject", "456");

                // Act & Assert
                Assert.NotEqual(sut1, sut2);
            }

            [Fact]
            public void Should_not_be_equal_when_schema_version_differs()
            {
                // Arrange
                var sut1 = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "v1" };
                var sut2 = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "v2" };

                // Act & Assert
                Assert.NotEqual(sut1, sut2);
            }
        }
    }
}
