#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using ErikLieben.FA.ES.S3.Configuration;

namespace ErikLieben.FA.ES.S3.Tests.Configuration;

public class EventStreamS3SettingsTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_when_default_data_store_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventStreamS3Settings(null!));
        }

        [Fact]
        public void Should_set_default_data_store()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Equal("s3", sut.DefaultDataStore);
        }

        [Fact]
        public void Should_set_default_bucket_name()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Equal("event-store", sut.BucketName);
        }

        [Fact]
        public void Should_default_document_store_to_default_data_store()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Equal("s3", sut.DefaultDocumentStore);
        }

        [Fact]
        public void Should_default_snapshot_store_to_default_data_store()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Equal("s3", sut.DefaultSnapShotStore);
        }

        [Fact]
        public void Should_default_document_tag_store_to_default_data_store()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Equal("s3", sut.DefaultDocumentTagStore);
        }

        [Fact]
        public void Should_use_custom_document_store_when_provided()
        {
            // Act
            var sut = new EventStreamS3Settings("s3", defaultDocumentStore: "custom-doc");

            // Assert
            Assert.Equal("custom-doc", sut.DefaultDocumentStore);
        }

        [Fact]
        public void Should_use_custom_snapshot_store_when_provided()
        {
            // Act
            var sut = new EventStreamS3Settings("s3", defaultSnapShotStore: "custom-snap");

            // Assert
            Assert.Equal("custom-snap", sut.DefaultSnapShotStore);
        }

        [Fact]
        public void Should_use_custom_document_tag_store_when_provided()
        {
            // Act
            var sut = new EventStreamS3Settings("s3", defaultDocumentTagStore: "custom-tag");

            // Assert
            Assert.Equal("custom-tag", sut.DefaultDocumentTagStore);
        }

        [Fact]
        public void Should_default_force_path_style_to_true()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.True(sut.ForcePathStyle);
        }

        [Fact]
        public void Should_default_region_to_us_east_1()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Equal("us-east-1", sut.Region);
        }

        [Fact]
        public void Should_default_auto_create_bucket_to_true()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.True(sut.AutoCreateBucket);
        }

        [Fact]
        public void Should_default_enable_stream_chunks_to_false()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.False(sut.EnableStreamChunks);
        }

        [Fact]
        public void Should_default_chunk_size_to_1000()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Equal(1000, sut.DefaultChunkSize);
        }

        [Fact]
        public void Should_default_document_container_name()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Equal("object-document-store", sut.DefaultDocumentContainerName);
        }

        [Fact]
        public void Should_set_service_url_when_provided()
        {
            // Act
            var sut = new EventStreamS3Settings("s3", serviceUrl: "http://localhost:9000");

            // Assert
            Assert.Equal("http://localhost:9000", sut.ServiceUrl);
        }

        [Fact]
        public void Should_set_access_and_secret_key_when_provided()
        {
            // Act
            var sut = new EventStreamS3Settings("s3", accessKey: "mykey", secretKey: "mysecret");

            // Assert
            Assert.Equal("mykey", sut.AccessKey);
            Assert.Equal("mysecret", sut.SecretKey);
        }

        [Fact]
        public void Should_set_max_connections_per_server_when_provided()
        {
            // Act
            var sut = new EventStreamS3Settings("s3", maxConnectionsPerServer: 50);

            // Assert
            Assert.Equal(50, sut.MaxConnectionsPerServer);
        }

        [Fact]
        public void Should_default_max_connections_per_server_to_null()
        {
            // Act
            var sut = new EventStreamS3Settings("s3");

            // Assert
            Assert.Null(sut.MaxConnectionsPerServer);
        }
    }
}
