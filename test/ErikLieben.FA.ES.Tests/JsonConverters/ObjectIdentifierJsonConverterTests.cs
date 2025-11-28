using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ErikLieben.FA.ES.JsonConverters;
using ErikLieben.FA.ES.VersionTokenParts;
using Xunit;

namespace ErikLieben.FA.ES.Tests.JsonConverters
{
    public class ObjectIdentifierJsonConverterTests
    {
        public class ReadMethod
        {
            [Fact]
            public void Should_correctly_deserialize_valid_string()
            {
                // Arrange
                var json = "\"oid[TestObject__123]v1\"";
                var converter = new ObjectIdentifierJsonConverter();
                var options = new JsonSerializerOptions();

                // Act
                var result = JsonSerializer.Deserialize<ObjectIdentifier>(json, options);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("TestObject", result.ObjectName);
                Assert.Equal("123", result.ObjectId);
                Assert.Equal("v1", result.SchemaVersion);
            }

            [Fact]
            public void Should_throw_exception_when_string_is_null_or_empty()
            {
                // Arrange
                var json = "\"\"";
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<ObjectIdentifier>(json, options));
                Assert.Contains("Invalid objectIdentifier format", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_when_string_does_not_start_with_prefix()
            {
                // Arrange
                var json = "\"invalid[TestObject__123]v1\"";
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<ObjectIdentifier>(json, options));
                Assert.Contains("Invalid objectIdentifier format", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_when_suffix_start_is_missing()
            {
                // Arrange
                var json = "\"oid[TestObject__123v1\"";
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<ObjectIdentifier>(json, options));
                Assert.Contains("Invalid objectIdentifier format", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_when_suffix_start_is_before_prefix()
            {
                // Arrange
                var json = "\"oid]TestObject__123[v1\"";
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<ObjectIdentifier>(json, options));
                Assert.Contains("Invalid objectIdentifier format", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_when_schema_version_is_missing()
            {
                // Arrange
                var json = "\"oid[TestObject__123]\"";
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<ObjectIdentifier>(json, options));
                Assert.Contains("Schema version missing", exception.Message);
            }
        }

        public class WriteMethod
        {
            [Fact]
            public void Should_correctly_serialize_object_identifier()
            {
                // Arrange
                var objectIdentifier = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "v1" };
                var options = new JsonSerializerOptions();

                // Act
                var json = JsonSerializer.Serialize(objectIdentifier, options);

                // Assert
                Assert.Equal("\"oid[TestObject__123]v1\"", json);
            }

            [Fact]
            public void Should_throw_exception_when_schema_version_is_null_or_empty()
            {
                // Arrange
                var objectIdentifier = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "" };
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    JsonSerializer.Serialize(objectIdentifier, options));
                Assert.Contains("SchemaVersion cannot be null or empty", exception.Message);
            }
        }

        public class ReadAsPropertyNameMethod
        {
            [Fact]
            public void Should_correctly_deserialize_property_name()
            {
                // Arrange
                var json = "{\"oid[TestObject__123]v1\": \"value\"}";
                var options = new JsonSerializerOptions();

                // Act
                var result = JsonSerializer.Deserialize<Dictionary<ObjectIdentifier, string>>(json, options);

                // Assert
                Assert.NotNull(result);
                Assert.Single(result);

                var key = result.Keys.First();
                Assert.Equal("TestObject", key.ObjectName);
                Assert.Equal("123", key.ObjectId);
                Assert.Equal("v1", key.SchemaVersion);
                Assert.Equal("value", result[key]);
            }

            [Fact]
            public void Should_throw_exception_when_property_name_is_invalid()
            {
                // Arrange
                var json = "{\"invalid[TestObject__123]v1\": \"value\"}";
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<Dictionary<ObjectIdentifier, string>>(json, options));
                Assert.Contains("Invalid objectIdentifier format as property name", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_when_property_name_suffix_start_is_missing()
            {
                // Arrange
                var json = "{\"oid[TestObject__123v1\": \"value\"}";
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<Dictionary<ObjectIdentifier, string>>(json, options));
                Assert.Contains("Invalid objectIdentifier format as property name", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_when_property_name_schema_version_is_missing()
            {
                // Arrange
                var json = "{\"oid[TestObject__123]\": \"value\"}";
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<JsonException>(() =>
                    JsonSerializer.Deserialize<Dictionary<ObjectIdentifier, string>>(json, options));
                Assert.Contains("Schema version missing in objectIdentifier", exception.Message);
            }
        }

        public class WriteAsPropertyNameMethod
        {
            [Fact]
            public void Should_correctly_serialize_dictionary_with_object_identifier_key()
            {
                // Arrange
                var objectIdentifier = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "v1" };
                var dictionary = new Dictionary<ObjectIdentifier, string>
                {
                    { objectIdentifier, "value" }
                };
                var options = new JsonSerializerOptions();

                // Act
                var json = JsonSerializer.Serialize(dictionary, options);

                // Assert
                Assert.Equal("{\"oid[TestObject__123]v1\":\"value\"}", json);
            }

            [Fact]
            public void Should_throw_exception_when_dictionary_key_schema_version_is_null_or_empty()
            {
                // Arrange
                var objectIdentifier = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "" };
                var dictionary = new Dictionary<ObjectIdentifier, string>
                {
                    { objectIdentifier, "value" }
                };
                var options = new JsonSerializerOptions();

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    JsonSerializer.Serialize(dictionary, options));
                Assert.Contains("SchemaVersion cannot be null or empty when writing an ObjectIdentifier as a dictionary key", exception.Message);
            }
        }

        public class EndToEndTests
        {
            [Fact]
            public void Should_correctly_roundtrip_object_identifier()
            {
                // Arrange
                var original = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "v1" };
                var options = new JsonSerializerOptions();

                // Act
                var json = JsonSerializer.Serialize(original, options);
                var deserialized = JsonSerializer.Deserialize<ObjectIdentifier>(json, options);

                // Assert
                Assert.NotNull(deserialized);
                Assert.Equal(original.ObjectName, deserialized.ObjectName);
                Assert.Equal(original.ObjectId, deserialized.ObjectId);
                Assert.Equal(original.SchemaVersion, deserialized.SchemaVersion);
                Assert.Equal(original.Value, deserialized.Value);
            }

            [Fact]
            public void Should_correctly_roundtrip_dictionary_with_object_identifier_key()
            {
                // Arrange
                var key = new ObjectIdentifier("TestObject", "123") { SchemaVersion = "v1" };
                var original = new Dictionary<ObjectIdentifier, string>
                {
                    { key, "value" }
                };
                var options = new JsonSerializerOptions();

                // Act
                var json = JsonSerializer.Serialize(original, options);
                var deserialized = JsonSerializer.Deserialize<Dictionary<ObjectIdentifier, string>>(json, options);

                // Assert
                Assert.NotNull(deserialized);
                Assert.Single(deserialized);

                var deserializedKey = deserialized.Keys.First();
                Assert.Equal(key.ObjectName, deserializedKey.ObjectName);
                Assert.Equal(key.ObjectId, deserializedKey.ObjectId);
                Assert.Equal(key.SchemaVersion, deserializedKey.SchemaVersion);
                Assert.Equal(key.Value, deserializedKey.Value);
                Assert.Equal("value", deserialized[deserializedKey]);
            }
        }
    }
}
