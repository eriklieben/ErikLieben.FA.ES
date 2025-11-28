using System;
using System.IO;
using System.Text;
using System.Text.Json;
using ErikLieben.FA.ES.JsonConverters;
using ErikLieben.FA.ES.VersionTokenParts;
using Xunit;

namespace ErikLieben.FA.ES.Tests.JsonConverters
{
    public class VersionIdentifierJsonConverterTests
    {
        public class ReadMethod
        {
            [Fact]
            public void Should_read_valid_version_identifier()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "\"vid[test_value__123]1.0\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act
                var result = sut.Read(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());

                // Assert
                Assert.NotNull(result);
                Assert.Equal("test_value__123", result.Value);
                Assert.Equal("1.0", result.SchemaVersion);
            }

            [Fact]
            public void Should_throw_json_exception_when_input_is_null()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "null";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    sut.Read(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionIdentifier format", exception.Message);
            }

            [Fact]
            public void Should_throw_json_exception_when_input_does_not_start_with_prefix()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "\"invalid_prefix[test_value]1.0\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    sut.Read(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionIdentifier format", exception.Message);
            }

            [Fact]
            public void Should_throw_json_exception_when_suffix_not_found()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "\"vid[test_value_without_suffix\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    sut.Read(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionIdentifier format", exception.Message);
            }

            [Fact]
            public void Should_throw_json_exception_when_schema_version_is_missing()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "\"vid[test_value]\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    sut.Read(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Schema version missing", exception.Message);
            }
        }

        public class WriteMethod
        {
            [Fact]
            public void Should_write_valid_version_identifier()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var versionIdentifier = new VersionIdentifier("test_value__123") { SchemaVersion = "1.0" };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);

                // Act
                sut.Write(writer, versionIdentifier, new JsonSerializerOptions());
                writer.Flush();
                stream.Position = 0;
                var result = Encoding.UTF8.GetString(stream.ToArray());

                // Assert
                Assert.Equal("\"vid[test_value__123]1.0\"", result);
            }

            [Fact]
            public void Should_throw_invalid_operation_exception_when_schema_version_is_null()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var versionIdentifier = new VersionIdentifier("test_value__123") { SchemaVersion = null! };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);

                // Act & Assert
                InvalidOperationException exception = null!;
                try
                {
                    sut.Write(writer, versionIdentifier, new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Equal("SchemaVersion cannot be null or empty when writing a VersionIdentifier.",
                    exception.Message);
            }

            [Fact]
            public void Should_throw_invalid_operation_exception_when_schema_version_is_empty()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var versionIdentifier = new VersionIdentifier("test_value__123") { SchemaVersion = string.Empty };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);

                // Act & Assert
                InvalidOperationException exception = null!;
                try
                {
                    sut.Write(writer, versionIdentifier, new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Equal("SchemaVersion cannot be null or empty when writing a VersionIdentifier.",
                    exception.Message);
            }
        }

        public class ReadAsPropertyNameMethod
        {
            [Fact]
            public void Should_read_valid_property_name()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "{\"vid[test_key__123]1.0\":\"value\"}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token (StartObject)
                reader.Read(); // Move to the property name

                // Act
                var result = sut.ReadAsPropertyName(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());

                // Assert
                Assert.NotNull(result);
                Assert.Equal("test_key__123", result.Value);
                Assert.Equal("1.0", result.SchemaVersion);
            }

            [Fact]
            public void Should_throw_json_exception_when_property_name_is_invalid()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "{\"invalid_property_name\":\"value\"}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token (StartObject)
                reader.Read(); // Move to the property name

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    sut.ReadAsPropertyName(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionIdentifier format as property name", exception.Message);
            }

            [Fact]
            public void Should_throw_json_exception_when_property_name_has_no_suffix()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "{\"vid[test_key_no_suffix\":\"value\"}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token (StartObject)
                reader.Read(); // Move to the property name

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    sut.ReadAsPropertyName(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionIdentifier format as property name", exception.Message);
            }

            [Fact]
            public void Should_throw_json_exception_when_property_name_has_no_schema_version()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var json = "{\"vid[test_key]\":\"value\"}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token (StartObject)
                reader.Read(); // Move to the property name

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    sut.ReadAsPropertyName(ref reader, typeof(VersionIdentifier), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Schema version missing in versionIdentifier", exception.Message);
            }
        }

        public class WriteAsPropertyNameMethod
        {
            [Fact]
            public void Should_write_valid_property_name()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var versionIdentifier = new VersionIdentifier("test_key__123") { SchemaVersion = "1.0" };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();

                // Act
                sut.WriteAsPropertyName(writer, versionIdentifier, new JsonSerializerOptions());
                writer.WriteStringValue("test_value");
                writer.WriteEndObject();
                writer.Flush();
                stream.Position = 0;
                var result = Encoding.UTF8.GetString(stream.ToArray());

                // Assert
                Assert.Equal("{\"vid[test_key__123]1.0\":\"test_value\"}", result);
            }

            [Fact]
            public void Should_throw_invalid_operation_exception_when_schema_version_is_null()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var versionIdentifier = new VersionIdentifier("test_key__123") { SchemaVersion = null! };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();

                // Act & Assert
                InvalidOperationException exception = null!;
                try
                {
                    sut.WriteAsPropertyName(writer, versionIdentifier, new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Equal(
                    "SchemaVersion cannot be null or empty when writing a VersionIdentifier as a dictionary key.",
                    exception.Message);
            }

            [Fact]
            public void Should_throw_invalid_operation_exception_when_schema_version_is_empty()
            {
                // Arrange
                var sut = new VersionIdentifierJsonConverter();
                var versionIdentifier = new VersionIdentifier("test_key__123") { SchemaVersion = string.Empty };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();

                // Act & Assert
                InvalidOperationException exception = null!;
                try
                {
                    sut.WriteAsPropertyName(writer, versionIdentifier, new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Equal(
                    "SchemaVersion cannot be null or empty when writing a VersionIdentifier as a dictionary key.",
                    exception.Message);
            }
        }
    }
}
