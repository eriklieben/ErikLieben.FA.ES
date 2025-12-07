using System;
using System.Text.Json;
using ErikLieben.FA.ES.Documents;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Documents
{
    public class StreamChunkTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_create_instance_with_default_values_when_using_parameterless_constructor()
            {
                // Arrange & Act
                var sut = new StreamChunk();

                // Assert
                Assert.Equal(0, sut.ChunkIdentifier);
                Assert.Null(sut.FirstEventVersion);
                Assert.Null(sut.LastEventVersion);
            }

            [Fact]
            public void Should_set_properties_when_using_parameterized_constructor()
            {
                // Arrange
                int chunkIdentifier = 42;
                int? firstEventVersion = 1;
                int? lastEventVersion = 100;

                // Act
                var sut = new StreamChunk(chunkIdentifier, firstEventVersion, lastEventVersion);

                // Assert
                Assert.Equal(chunkIdentifier, sut.ChunkIdentifier);
                Assert.Equal(firstEventVersion, sut.FirstEventVersion);
                Assert.Equal(lastEventVersion, sut.LastEventVersion);
            }

            [Fact]
            public void Should_accept_null_for_version_parameters_when_using_parameterized_constructor()
            {
                // Arrange
                int chunkIdentifier = 42;
                int? firstEventVersion = null;
                int? lastEventVersion = null;

                // Act
                var sut = new StreamChunk(chunkIdentifier, firstEventVersion, lastEventVersion);

                // Assert
                Assert.Equal(chunkIdentifier, sut.ChunkIdentifier);
                Assert.Null(sut.FirstEventVersion);
                Assert.Null(sut.LastEventVersion);
            }
        }

        public class Properties
        {
            [Fact]
            public void Should_set_and_get_chunk_identifier()
            {
                // Arrange
                var sut = new StreamChunk();
                int expectedValue = 42;

                // Act
                sut.ChunkIdentifier = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.ChunkIdentifier);
            }

            [Fact]
            public void Should_set_and_get_first_event_version()
            {
                // Arrange
                var sut = new StreamChunk();
                int? expectedValue = 42;

                // Act
                sut.FirstEventVersion = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.FirstEventVersion);
            }

            [Fact]
            public void Should_set_and_get_last_event_version()
            {
                // Arrange
                var sut = new StreamChunk();
                int? expectedValue = 42;

                // Act
                sut.LastEventVersion = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.LastEventVersion);
            }
        }

        public class JsonSerialization
        {
            private static readonly JsonSerializerOptions DefaultOptions = new();
            private static readonly JsonSerializerOptions NotIndentedOptions = new() { WriteIndented = false };

            [Fact]
            public void Should_serialize_with_correct_property_names()
            {
                // Arrange
                var sut = new StreamChunk(42, 1, 100);

                // Act
                var json = JsonSerializer.Serialize(sut, DefaultOptions);

                // Assert
                Assert.Contains("\"id\":42", json);
                Assert.Contains("\"first\":1", json);
                Assert.Contains("\"last\":100", json);
            }

            [Fact]
            public void Should_serialize_chunk_identifier_even_when_its_value_is_default()
            {
                // Arrange
                var sut = new StreamChunk(0, null, null);

                // Act
                var json = JsonSerializer.Serialize(sut, DefaultOptions);

                // Assert
                Assert.Contains("\"id\":0", json);
            }

            [Fact]
            public void Should_deserialize_from_json_with_custom_property_names()
            {
                // Arrange
                string json = "{\"id\":42,\"first\":1,\"last\":100}";

                // Act
                var result = JsonSerializer.Deserialize<StreamChunk>(json, DefaultOptions);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(42, result.ChunkIdentifier);
                Assert.Equal(1, result.FirstEventVersion);
                Assert.Equal(100, result.LastEventVersion);
            }

            [Fact]
            public void Should_serialize_properties_in_correct_order()
            {
                // Arrange
                var sut = new StreamChunk(42, 1, 100);

                // Act
                var json = JsonSerializer.Serialize(sut, NotIndentedOptions);

                // Assert
                // Check that id appears before first, and first appears before last
                int idPos = json.IndexOf("\"id\"", StringComparison.Ordinal);
                int firstPos = json.IndexOf("\"first\"", StringComparison.Ordinal);
                int lastPos = json.IndexOf("\"last\"", StringComparison.Ordinal);

                Assert.True(idPos < firstPos);
                Assert.True(firstPos < lastPos);
            }

            [Fact]
            public void Should_handle_null_version_values_during_serialization()
            {
                // Arrange
                var sut = new StreamChunk(42, null, null);

                // Act
                var json = JsonSerializer.Serialize(sut, DefaultOptions);

                // Assert
                Assert.Contains("\"id\":42", json);
                Assert.Contains("\"first\":null", json);
                Assert.Contains("\"last\":null", json);
            }
        }
    }
}
