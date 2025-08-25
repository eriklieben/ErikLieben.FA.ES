using ErikLieben.FA.ES.Documents;

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
                    SnapShots = new List<StreamSnapShot>()
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
