using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Documents
{
    public class ObjectDocumentTests
    {
        private class TestObjectDocument : ObjectDocument
        {
            public TestObjectDocument(
                string objectId,
                string objectName,
                StreamInformation active,
                IEnumerable<TerminatedStream> terminatedStreams,
                string? schemaVersion = null,
                string? hash = null,
                string? prevHash = null)
                : base(objectId, objectName, active, terminatedStreams, schemaVersion, hash, prevHash)
            {
            }
        }

        public class Constructor
        {
            [Fact]
            public void Should_initialize_properties_correctly()
            {
                // Arrange
                var objectId = "test-id";
                var objectName = "test-name";
                var active = Substitute.For<StreamInformation>();
                var terminatedStreams = new List<TerminatedStream> { new TerminatedStream() };
                var schemaVersion = "1.0";
                var hash = "hash-value";
                var prevHash = "prev-hash-value";

                // Act
                var sut = new TestObjectDocument(
                    objectId,
                    objectName,
                    active,
                    terminatedStreams,
                    schemaVersion,
                    hash,
                    prevHash);

                // Assert
                Assert.Equal(objectId, sut.ObjectId);
                Assert.Equal(objectName, sut.ObjectName);
                Assert.Same(active, sut.Active);
                Assert.Equal(terminatedStreams.Count, sut.TerminatedStreams.Count);
                Assert.Same(terminatedStreams.First(), sut.TerminatedStreams.First());
                Assert.Equal(schemaVersion, sut.SchemaVersion);
                Assert.Equal(hash, sut.Hash);
                Assert.Equal(prevHash, sut.PrevHash);
            }

            [Fact]
            public void Should_initialize_properties_with_default_values()
            {
                // Arrange
                var objectId = "test-id";
                var objectName = "test-name";
                var active = Substitute.For<StreamInformation>();
                var terminatedStreams = new List<TerminatedStream>();

                // Act
                var sut = new TestObjectDocument(
                    objectId,
                    objectName,
                    active,
                    terminatedStreams);

                // Assert
                Assert.Equal(objectId, sut.ObjectId);
                Assert.Equal(objectName, sut.ObjectName);
                Assert.Same(active, sut.Active);
                Assert.Empty(sut.TerminatedStreams);
                Assert.Null(sut.SchemaVersion);
                Assert.Null(sut.Hash);
                Assert.Null(sut.PrevHash);
            }

            [Fact]
            public void Should_throw_when_object_id_is_null()
            {
                // Arrange
                string? objectId = null;
                var objectName = "test-name";
                var active = Substitute.For<StreamInformation>();
                var terminatedStreams = new List<TerminatedStream>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new TestObjectDocument(
                    objectId!,
                    objectName,
                    active,
                    terminatedStreams));
            }

            [Fact]
            public void Should_throw_when_object_name_is_null()
            {
                // Arrange
                var objectId = "test-id";
                string? objectName = null;
                var active = Substitute.For<StreamInformation>();;
                var terminatedStreams = new List<TerminatedStream>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new TestObjectDocument(
                    objectId,
                    objectName!,
                    active,
                    terminatedStreams));
            }

            [Fact]
            public void Should_throw_when_active_is_null()
            {
                // Arrange
                var objectId = "test-id";
                var objectName = "test-name";
                StreamInformation? active = null;
                var terminatedStreams = new List<TerminatedStream>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new TestObjectDocument(
                    objectId,
                    objectName,
                    active!,
                    terminatedStreams));
            }

            [Fact]
            public void Should_throw_when_terminated_streams_is_null()
            {
                // Arrange
                var objectId = "test-id";
                var objectName = "test-name";
                var active = Substitute.For<StreamInformation>();
                IEnumerable<TerminatedStream>? terminatedStreams = null;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new TestObjectDocument(
                    objectId,
                    objectName,
                    active,
                    terminatedStreams!));
            }
        }

        public class SetHash
        {
            [Fact]
            public void Should_set_hash_and_prev_hash()
            {
                // Arrange
                var sut = new TestObjectDocument(
                    "test-id",
                    "test-name",
                    Substitute.For<StreamInformation>(),
                    new List<TerminatedStream>());
                var hash = "new-hash";
                var prevHash = "new-prev-hash";

                // Act
                sut.SetHash(hash, prevHash);

                // Assert
                Assert.Equal(hash, sut.Hash);
                Assert.Equal(prevHash, sut.PrevHash);
            }

            [Fact]
            public void Should_set_hash_with_null_prev_hash()
            {
                // Arrange
                var sut = new TestObjectDocument(
                    "test-id",
                    "test-name",
                    Substitute.For<StreamInformation>(),
                    new List<TerminatedStream>(),
                    null,
                    "initial-hash",
                    "initial-prev-hash");
                var hash = "new-hash";

                // Act
                sut.SetHash(hash);

                // Assert
                Assert.Equal(hash, sut.Hash);
                Assert.Null(sut.PrevHash);
            }

            [Fact]
            public void Should_set_null_hash_and_prev_hash()
            {
                // Arrange
                var sut = new TestObjectDocument(
                    "test-id",
                    "test-name",
                    Substitute.For<StreamInformation>(),
                    new List<TerminatedStream>(),
                    null,
                    "initial-hash",
                    "initial-prev-hash");

                // Act
                sut.SetHash(null);

                // Assert
                Assert.Null(sut.Hash);
                Assert.Null(sut.PrevHash);
            }
        }

        public class Properties
        {
            [Fact]
            public void Should_return_correct_object_id()
            {
                // Arrange
                var objectId = "test-id";
                var sut = new TestObjectDocument(
                    objectId,
                    "test-name",
                    Substitute.For<StreamInformation>(),
                    new List<TerminatedStream>());

                // Act
                var result = sut.ObjectId;

                // Assert
                Assert.Equal(objectId, result);
            }

            [Fact]
            public void Should_have_json_property_name_attributes()
            {
                // Arrange & Act
                var activeProperty = typeof(ObjectDocument).GetProperty(nameof(ObjectDocument.Active));
                var objectIdProperty = typeof(ObjectDocument).GetProperty(nameof(ObjectDocument.ObjectId));
                var objectNameProperty = typeof(ObjectDocument).GetProperty(nameof(ObjectDocument.ObjectName));
                var terminatedStreamsProperty = typeof(ObjectDocument).GetProperty(nameof(ObjectDocument.TerminatedStreams));
                var schemaVersionProperty = typeof(ObjectDocument).GetProperty(nameof(ObjectDocument.SchemaVersion));
                var hashProperty = typeof(ObjectDocument).GetProperty(nameof(ObjectDocument.Hash));
                var prevHashProperty = typeof(ObjectDocument).GetProperty(nameof(ObjectDocument.PrevHash));

                // Assert
                Assert.NotNull(activeProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                    .Cast<JsonPropertyNameAttribute>()
                    .SingleOrDefault(attr => attr.Name == "active"));

                Assert.NotNull(objectIdProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                    .Cast<JsonPropertyNameAttribute>()
                    .SingleOrDefault(attr => attr.Name == "objectId"));

                Assert.NotNull(objectNameProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                    .Cast<JsonPropertyNameAttribute>()
                    .SingleOrDefault(attr => attr.Name == "objectName"));

                Assert.NotNull(terminatedStreamsProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                    .Cast<JsonPropertyNameAttribute>()
                    .SingleOrDefault(attr => attr.Name == "terminatedStreams"));

                Assert.NotNull(schemaVersionProperty?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                    .Cast<JsonPropertyNameAttribute>()
                    .SingleOrDefault(attr => attr.Name == "schemaVersion"));

                Assert.NotNull(hashProperty?.GetCustomAttributes(typeof(JsonIgnoreAttribute), false)
                    .Cast<JsonIgnoreAttribute>()
                    .SingleOrDefault());

                Assert.NotNull(prevHashProperty?.GetCustomAttributes(typeof(JsonIgnoreAttribute), false)
                    .Cast<JsonIgnoreAttribute>()
                    .SingleOrDefault());
            }
        }
    }
}
