using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.VersionTokenParts;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.VersionTokenParts
{
    public class VersionIdentifierTests
    {
        public class Ctor
        {
            [Fact]
            public void Should_initialize_with_default_values_when_using_parameterless_constructor()
            {
                // Arrange & Act
                var sut = new VersionIdentifier();

                // Assert
                Assert.Equal(string.Empty, sut.StreamIdentifier);
                Assert.Equal(string.Empty, sut.VersionString);
                Assert.Equal("v1", sut.SchemaVersion);
                Assert.Equal("__", sut.Value);
            }

            [Fact]
            public void Should_initialize_from_stream_identifier_and_version()
            {
                // Arrange
                var streamIdentifier = "TestStream";
                var version = 42;

                // Act
                var sut = new VersionIdentifier(streamIdentifier, version);

                // Assert
                Assert.Equal(streamIdentifier, sut.StreamIdentifier);
                Assert.Equal("00000000000000000042", sut.VersionString);
                Assert.Equal("v1", sut.SchemaVersion);
                Assert.Equal("TestStream__00000000000000000042", sut.Value);
            }

            [Fact]
            public void Should_throw_ArgumentNullException_when_stream_identifier_is_null()
            {
                // Arrange
                string? streamIdentifier = null;
                var version = 42;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new VersionIdentifier(streamIdentifier!, version));
            }

            [Fact]
            public void Should_initialize_from_version_token_string()
            {
                // Arrange
                var versionTokenString = "TestStream__00000000000000000042";

                // Act
                var sut = new VersionIdentifier(versionTokenString);

                // Assert
                Assert.Equal("TestStream", sut.StreamIdentifier);
                Assert.Equal("00000000000000000042", sut.VersionString);
                Assert.Equal("TestStream__00000000000000000042", sut.Value);
            }

            [Fact]
            public void Should_throw_argumentnullexception_when_version_token_string_is_null()
            {
                // Arrange
                string? versionTokenString = null;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new VersionIdentifier(versionTokenString!));
            }

            [Fact]
            public void Should_throw_argumentexception_when_version_token_string_has_incorrect_format()
            {
                // Arrange
                var versionTokenString = "InvalidFormatString";

                // Act & Assert
                var exception = Assert.Throws<ArgumentException>(() => new VersionIdentifier(versionTokenString));
                Assert.Contains("IdentifierString must consist out if 2 parts split by __", exception.Message);
                Assert.Contains(versionTokenString, exception.Message);
            }

            [Fact]
            public void Should_throw_argumentexception_when_version_token_string_has_empty_parts()
            {
                // Arrange
                var versionTokenString = "__";

                // Act & Assert
                var exception = Assert.Throws<ArgumentException>(() => new VersionIdentifier(versionTokenString));
                Assert.Contains("IdentifierString must consist out if 2 parts split by __", exception.Message);
                Assert.Contains(versionTokenString, exception.Message);
            }
        }

        public class ValueProperty
        {
            [Fact]
            public void Should_combine_stream_identifier_and_version_string()
            {
                // Arrange
                var sut = new VersionIdentifier("TestStream", 42);

                // Act
                var result = sut.Value;

                // Assert
                Assert.Equal("TestStream__00000000000000000042", result);
            }
        }

        public class ToStringMethod
        {
            [Fact]
            public void Should_return_value_property()
            {
                // Arrange
                var sut = new VersionIdentifier("TestStream", 42);

                // Act
                var result = sut.ToString();

                // Assert
                Assert.Equal(sut.Value, result);
                Assert.Equal("TestStream__00000000000000000042", result);
            }
        }

        public class CompareToMethod
        {
            [Fact]
            public void Should_compare_value_properties_when_comparing_version_identifiers()
            {
                // Arrange
                var sut = new VersionIdentifier("TestStream", 42);
                var other = new VersionIdentifier("TestStream", 42);
                var earlier = new VersionIdentifier("TestStream", 41);
                var later = new VersionIdentifier("TestStream", 43);
                var differentStream = new VersionIdentifier("OtherStream", 42);

                // Act & Assert
                Assert.Equal(0, sut.CompareTo(other));
                Assert.True(sut.CompareTo(earlier) > 0);
                Assert.True(sut.CompareTo(later) < 0);
                Assert.NotEqual(0, sut.CompareTo(differentStream));
            }

            [Fact]
            public void Should_handle_null_when_comparing_version_identifiers()
            {
                // Arrange
                var sut = new VersionIdentifier("TestStream", 42);
                VersionIdentifier? nullIdentifier = null;

                // Act
                var result = sut.CompareTo(nullIdentifier);

                // Assert
                Assert.True(result > 0);
            }

            [Fact]
            public void Should_compare_value_properties_when_comparing_to_object()
            {
                // Arrange
                var sut = new VersionIdentifier("TestStream", 42);
                object other = new VersionIdentifier("TestStream", 42);
                object earlier = new VersionIdentifier("TestStream", 41);
                object later = new VersionIdentifier("TestStream", 43);

                // Act & Assert
                Assert.Equal(0, sut.CompareTo(other));
                Assert.True(sut.CompareTo(earlier) > 0);
                Assert.True(sut.CompareTo(later) < 0);
            }

            [Fact]
            public void Should_handle_null_when_comparing_to_object()
            {
                // Arrange
                var sut = new VersionIdentifier("TestStream", 42);
                object? nullObject = null;

                // Act
                var result = sut.CompareTo(nullObject);

                // Assert
                Assert.True(result > 0);
            }

            [Fact]
            public void Should_handle_non_version_identifier_object_when_comparing()
            {
                // Arrange
                var sut = new VersionIdentifier("TestStream", 42);
                object nonVersionIdentifier = "Not a VersionIdentifier";

                // Act
                var result = sut.CompareTo(nonVersionIdentifier);

                // Assert
                Assert.True(result > 0);
            }
        }

        public class SchemaVersionProperty
        {
            [Fact]
            public void Should_have_default_value_v1()
            {
                // Arrange & Act
                var sut = new VersionIdentifier();

                // Assert
                Assert.Equal("v1", sut.SchemaVersion);
            }

            [Fact]
            public void Should_allow_setting_custom_schema_version()
            {
                // Arrange
                var customSchemaVersion = "v2";

                // Act
                var sut = new VersionIdentifier { SchemaVersion = customSchemaVersion };

                // Assert
                Assert.Equal(customSchemaVersion, sut.SchemaVersion);
            }
        }

        public class RecordBehavior
        {
            [Fact]
            public void Should_have_value_equality()
            {
                // Arrange
                var sut1 = new VersionIdentifier("TestStream", 42);
                var sut2 = new VersionIdentifier("TestStream", 42);
                var different = new VersionIdentifier("TestStream", 43);

                // Act & Assert
                Assert.Equal(sut1, sut2);
                Assert.NotEqual(sut1, different);
            }

            [Fact]
            public void Should_have_equal_hash_codes_for_equal_instances()
            {
                // Arrange
                var sut1 = new VersionIdentifier("TestStream", 42);
                var sut2 = new VersionIdentifier("TestStream", 42);

                // Act & Assert
                Assert.Equal(sut1.GetHashCode(), sut2.GetHashCode());
            }
        }


    }
}
