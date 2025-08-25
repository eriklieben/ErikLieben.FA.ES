using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;
using Xunit;

namespace ErikLieben.FA.ES.Tests.JsonConverters
{
    public class VersionTokenJsonConverterTests
    {
        public class ReadMethod
        {
            [Fact]
            public void Should_deserialize_valid_version_token()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "\"vt[account__1234__0001__0002]v1\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act
                var sut = converter.Read(ref reader, typeof(VersionToken), new JsonSerializerOptions());

                // Assert
                Assert.Equal("account__1234__0001__0002", sut.Value);
                Assert.Equal("v1", sut.SchemaVersion);
            }

            [Fact]
            public void Should_deserialize_valid_version_token_with_legacy_prefix()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "\"versionToken[account__1234__0001__0002]v1\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act
                var sut = converter.Read(ref reader, typeof(VersionToken), new JsonSerializerOptions());

                // Assert
                Assert.Equal("account__1234__0001__0002", sut.Value);
                Assert.Equal("v1", sut.SchemaVersion);
            }

            [Fact]
            public void Should_throw_exception_for_null_input()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "null";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.Read(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionToken format", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_for_empty_string()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "\"\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.Read(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionToken format", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_for_invalid_prefix()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "\"invalid[testValue]v1\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.Read(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionToken format", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_for_missing_suffix()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "\"vt[testValue\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.Read(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionToken format", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_for_missing_schema_version()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "\"vt[testValue]\"";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Move to the first token

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.Read(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Schema version missing in versionToken", exception.Message);
            }
        }

        public class WriteMethod
        {
            [Fact]
            public void Should_serialize_version_token_correctly()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var versionToken = new VersionToken("account__1234__0001__0002") { SchemaVersion = "v1" };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);

                // Act
                converter.Write(writer, versionToken, new JsonSerializerOptions());
                writer.Flush();
                stream.Position = 0;
                var json = Encoding.UTF8.GetString(stream.ToArray());

                // Assert
                Assert.Equal("\"vt[account__1234__0001__0002]v1\"", json);
            }

            [Fact]
            public void Should_throw_exception_for_missing_schema_version()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var versionToken = new VersionToken("account__1234__0001__0002") { SchemaVersion = "" };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);

                // Act & Assert
                InvalidOperationException exception = null!;
                try
                {
                    converter.Write(writer, versionToken, new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Equal("SchemaVersion cannot be null or empty when writing a VersionToken.", exception.Message);
            }
        }

        public class ReadAsPropertyNameMethod
        {
            [Fact]
            public void Should_deserialize_valid_version_token_as_property_name()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "{\"vt[account__1234__0001__0002]v1\":null}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read();
                reader.Read();

                // Act
                var sut = converter.ReadAsPropertyName(ref reader, typeof(VersionToken), new JsonSerializerOptions());

                // Assert
                Assert.Equal("account__1234__0001__0002", sut.Value);
                Assert.Equal("v1", sut.SchemaVersion);
            }

            [Fact]
            public void Should_deserialize_valid_version_token_with_legacy_prefix_as_property_name()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "{\"versionToken[account__1234__0001__0002]v1\":null}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Object start
                reader.Read(); // Property name

                // Act
                var sut = converter.ReadAsPropertyName(ref reader, typeof(VersionToken), new JsonSerializerOptions());

                // Assert
                Assert.Equal("account__1234__0001__0002", sut.Value);
                Assert.Equal("v1", sut.SchemaVersion);
            }

            [Fact]
            public void Should_throw_exception_for_empty_string_as_property_name()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "{\"\":null}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Object start
                reader.Read(); // Property name

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.ReadAsPropertyName(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionToken format as property name", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_for_invalid_prefix_as_property_name()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "{\"invalid[account__1234__0001__0002]v1\":null}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Object start
                reader.Read(); // Property name

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.ReadAsPropertyName(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionToken format as property name", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_for_missing_suffix_as_property_name()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "{\"vt[account__1234__0001__0002\":null}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Object start
                reader.Read(); // Property name

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.ReadAsPropertyName(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Invalid versionToken format as property name", exception.Message);
            }

            [Fact]
            public void Should_throw_exception_for_missing_schema_version_as_property_name()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var json = "{\"vt[account__1234__0001__0002]\":null}";
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                reader.Read(); // Object start
                reader.Read(); // Property name

                // Act & Assert
                JsonException exception = null!;
                try
                {
                    converter.ReadAsPropertyName(ref reader, typeof(VersionToken), new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (JsonException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Contains("Schema version missing in versionToken", exception.Message);
            }
        }

        public class WriteAsPropertyNameMethod
        {
            [Fact]
            public void Should_serialize_version_token_as_property_name_correctly()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var versionToken = new VersionToken("account__1234__0001__0002") { SchemaVersion = "v1" };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();

                // Act
                converter.WriteAsPropertyName(writer, versionToken, new JsonSerializerOptions());
                writer.WriteNullValue();
                writer.WriteEndObject();
                writer.Flush();
                stream.Position = 0;
                var json = Encoding.UTF8.GetString(stream.ToArray());

                // Assert
                Assert.Equal("{\"vt[account__1234__0001__0002]v1\":null}", json);
            }

            [Fact]
            public void Should_throw_exception_for_missing_schema_version_as_property_name()
            {
                // Arrange
                var converter = new VersionTokenJsonConverter();
                var versionToken = new VersionToken("account__1234__0001__0002") { SchemaVersion = "" };
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();

                // Act & Assert
                InvalidOperationException exception = null!;
                try
                {
                    converter.WriteAsPropertyName(writer, versionToken, new JsonSerializerOptions());
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                }

                Assert.NotNull(exception);
                Assert.Equal("SchemaVersion cannot be null or empty when writing a VersionToken as a dictionary key.", exception.Message);
            }
        }

        [Fact]
        public void Should_validate_end_to_end_serialization_and_deserialization()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new VersionTokenJsonConverter());
            var original = new VersionToken("account__1234__0001__0002") { SchemaVersion = "v1" };

            // Act
            var json = JsonSerializer.Serialize(original, options);
            var deserialized = JsonSerializer.Deserialize<VersionToken>(json, options);

            // Assert
            Assert.Equal(original.Value, deserialized?.Value);
            Assert.Equal(original.SchemaVersion, deserialized?.SchemaVersion);
        }

        [Fact]
        public void Should_validate_end_to_end_dictionary_serialization_and_deserialization()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new VersionTokenJsonConverter());
            var key = new VersionToken("account__1234__0001__0002") { SchemaVersion = "v1" };
            var dictionary = new System.Collections.Generic.Dictionary<VersionToken, string> { { key, "value" } };

            // Act
            var json = JsonSerializer.Serialize(dictionary, options);
            var deserialized = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<VersionToken, string>>(json, options);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Single(deserialized);
            Assert.True(deserialized.ContainsKey(key));
            Assert.Equal("value", deserialized[key]);
        }
    }
}
