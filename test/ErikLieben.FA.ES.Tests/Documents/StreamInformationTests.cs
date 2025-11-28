using System.Collections.Generic;
using ErikLieben.FA.ES.Documents;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Documents
{
    public class StreamInformationTests
    {
        public class Properties
        {
            [Fact]
            public void Should_set_and_get_required_properties()
            {
                // Arrange
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection"
                };

                // Act & Assert
                Assert.Equal("stream-123", sut.StreamIdentifier);
                Assert.Equal("event-stream", sut.StreamType);
                Assert.Equal("document-tag", sut.DocumentTagType);
                Assert.Equal(5, sut.CurrentStreamVersion);
                Assert.Equal("stream-connection", sut.StreamConnectionName);
                Assert.Equal("document-tag-connection", sut.DocumentTagConnectionName);
                Assert.Equal("stream-tag-connection", sut.StreamTagConnectionName);
                Assert.Equal("snapshot-connection", sut.SnapShotConnectionName);
            }

            [Fact]
            public void Should_set_and_get_new_provider_type_properties()
            {
                // Arrange
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "blob",
                    DocumentType = "cosmos",
                    DocumentTagType = "redis",
                    EventStreamTagType = "memory",
                    DocumentRefType = "sql",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection"
                };

                // Act & Assert
                Assert.Equal("blob", sut.StreamType);
                Assert.Equal("cosmos", sut.DocumentType);
                Assert.Equal("redis", sut.DocumentTagType);
                Assert.Equal("memory", sut.EventStreamTagType);
                Assert.Equal("sql", sut.DocumentRefType);
            }

            [Fact]
            public void Should_set_and_get_new_named_connection_properties()
            {
                // Arrange
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "blob",
                    DocumentType = "blob",
                    DocumentTagType = "blob",
                    EventStreamTagType = "blob",
                    DocumentRefType = "blob",
                    CurrentStreamVersion = 5,
                    DataStore = "Store1",
                    DocumentStore = "Store2",
                    DocumentTagStore = "Store3",
                    StreamTagStore = "Store4",
                    SnapShotStore = "Store5",
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection"
                };

                // Act & Assert
                Assert.Equal("Store1", sut.DataStore);
                Assert.Equal("Store2", sut.DocumentStore);
                Assert.Equal("Store3", sut.DocumentTagStore);
                Assert.Equal("Store4", sut.StreamTagStore);
                Assert.Equal("Store5", sut.SnapShotStore);
            }

            [Fact]
            public void Should_initialize_new_properties_with_empty_strings()
            {
                // Arrange & Act
                var sut = new StreamInformation();

                // Assert
                Assert.Equal(string.Empty, sut.DocumentType);
                Assert.Equal(string.Empty, sut.EventStreamTagType);
                Assert.Equal(string.Empty, sut.DocumentRefType);
                Assert.Equal(string.Empty, sut.DataStore);
                Assert.Equal(string.Empty, sut.DocumentStore);
                Assert.Equal(string.Empty, sut.DocumentTagStore);
                Assert.Equal(string.Empty, sut.StreamTagStore);
                Assert.Equal(string.Empty, sut.SnapShotStore);
            }

            [Fact]
            public void Should_support_mixed_provider_types_scenario()
            {
                // Arrange - Blob for events, Cosmos for documents
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "blob",
                    DocumentType = "cosmos",
                    DocumentTagType = "cosmos",
                    EventStreamTagType = "blob",
                    DocumentRefType = "cosmos",
                    CurrentStreamVersion = 0,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection"
                };

                // Act & Assert
                Assert.Equal("blob", sut.StreamType);
                Assert.Equal("cosmos", sut.DocumentType);
                Assert.Equal("cosmos", sut.DocumentTagType);
                Assert.Equal("blob", sut.EventStreamTagType);
                Assert.Equal("cosmos", sut.DocumentRefType);
            }

            [Fact]
            public void Should_support_performance_tiering_scenario()
            {
                // Arrange - High throughput for data, cold storage for snapshots
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "blob",
                    CurrentStreamVersion = 0,
                    DataStore = "HighThroughputStore",
                    SnapShotStore = "ColdStore",
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection",
                    DocumentStore = string.Empty,
                    DocumentTagStore = string.Empty,
                    StreamTagStore = string.Empty
                };

                // Act & Assert
                Assert.Equal("HighThroughputStore", sut.DataStore);
                Assert.Equal("ColdStore", sut.SnapShotStore);
                Assert.Equal(string.Empty, sut.DocumentStore);
            }

            [Fact]
            public void Should_initialize_empty_collections()
            {
                // Arrange & Act
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection"
                };

                // Assert
                Assert.NotNull(sut.StreamChunks);
                Assert.Empty(sut.StreamChunks);
                Assert.NotNull(sut.SnapShots);
                Assert.Empty(sut.SnapShots);
            }

            [Fact]
            public void Should_set_and_get_optional_chunk_settings()
            {
                // Arrange
                var chunkSettings = new StreamChunkSettings
                {
                    EnableChunks = true,
                    ChunkSize = 100
                };

                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection",
                    ChunkSettings = chunkSettings
                };

                // Act & Assert
                Assert.NotNull(sut.ChunkSettings);
                Assert.True(sut.ChunkSettings.EnableChunks);
                Assert.Equal(100, sut.ChunkSettings.ChunkSize);
            }
        }

        public class ChunkingEnabledMethod
        {
            [Fact]
            public void Should_return_true_when_chunk_settings_enable_chunks()
            {
                // Arrange
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection",
                    ChunkSettings = new StreamChunkSettings
                    {
                        EnableChunks = true,
                        ChunkSize = 100
                    }
                };

                // Act
                var result = sut.ChunkingEnabled();

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void Should_return_false_when_chunk_settings_disable_chunks()
            {
                // Arrange
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection",
                    ChunkSettings = new StreamChunkSettings
                    {
                        EnableChunks = false,
                        ChunkSize = 100
                    }
                };

                // Act
                var result = sut.ChunkingEnabled();

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void Should_return_false_when_chunk_settings_is_null()
            {
                // Arrange
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection",
                    ChunkSettings = null
                };

                // Act
                var result = sut.ChunkingEnabled();

                // Assert
                Assert.False(result);
            }
        }

        public class HasSnapShotsMethod
        {
            [Fact]
            public void Should_return_true_when_snapshots_exist()
            {
                // Arrange
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection",
                    SnapShots = [new StreamSnapShot
                    {
                        Name = "1",
                        UntilVersion = 1
                    }]
                };

                // Act
                var result = sut.HasSnapShots();

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void Should_return_false_when_snapshots_collection_is_empty()
            {
                // Arrange
                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection",
                    SnapShots = []
                };

                // Act
                var result = sut.HasSnapShots();

                // Assert
                Assert.False(result);
            }
        }

        public class StreamChunksCollection
        {
            [Fact]
            public void Should_add_stream_chunks_to_collection()
            {
                // Arrange
                var chunk1 = new StreamChunk();
                var chunk2 = new StreamChunk();

                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection"
                };

                // Act
                sut.StreamChunks.Add(chunk1);
                sut.StreamChunks.Add(chunk2);

                // Assert
                Assert.Equal(2, sut.StreamChunks.Count);
                Assert.Contains(chunk1, sut.StreamChunks);
                Assert.Contains(chunk2, sut.StreamChunks);
            }
        }

        public class SnapShotsCollection
        {
            [Fact]
            public void Should_add_snapshots_to_collection()
            {
                // Arrange
                var snapshot1 = new StreamSnapShot
                {
                    Name = "1",
                    UntilVersion = 1
                };
                var snapshot2 = new StreamSnapShot
                {
                    Name = "2",
                    UntilVersion = 2
                };

                var sut = new StreamInformation
                {
                    StreamIdentifier = "stream-123",
                    StreamType = "event-stream",
                    DocumentTagType = "document-tag",
                    CurrentStreamVersion = 5,
                    StreamConnectionName = "stream-connection",
                    DocumentTagConnectionName = "document-tag-connection",
                    StreamTagConnectionName = "stream-tag-connection",
                    SnapShotConnectionName = "snapshot-connection"
                };

                // Act
                sut.SnapShots.Add(snapshot1);
                sut.SnapShots.Add(snapshot2);

                // Assert
                Assert.Equal(2, sut.SnapShots.Count);
                Assert.Contains(snapshot1, sut.SnapShots);
                Assert.Contains(snapshot2, sut.SnapShots);
            }
        }
    }
}
