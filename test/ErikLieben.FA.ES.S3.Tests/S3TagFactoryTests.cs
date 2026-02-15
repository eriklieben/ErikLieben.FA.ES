using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3TagFactoryTests
{
    private static EventStreamS3Settings CreateS3Settings() =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret");

    private static EventStreamDefaultTypeSettings CreateTypeSettings() =>
        new()
        {
            DocumentTagType = "s3",
            EventStreamTagType = "s3",
            StreamType = "s3",
            DocumentType = "s3",
            DocumentRefType = "s3"
        };

    public class CreateDocumentTagStoreFromDocument
    {
        [Fact]
        public void Should_throw_when_document_is_null()
        {
            var sut = new S3TagFactory(
                Substitute.For<IS3ClientFactory>(),
                CreateTypeSettings(),
                CreateS3Settings());

            Assert.Throws<ArgumentNullException>(() => sut.CreateDocumentTagStore((IObjectDocument)null!));
        }

        [Fact]
        public void Should_return_s3_document_tag_store()
        {
            var doc = Substitute.For<IObjectDocument>();
            var streamInfo = new StreamInformation { DocumentTagType = "s3" };
            doc.Active.Returns(streamInfo);

            var sut = new S3TagFactory(
                Substitute.For<IS3ClientFactory>(),
                CreateTypeSettings(),
                CreateS3Settings());

            var result = sut.CreateDocumentTagStore(doc);

            Assert.NotNull(result);
            Assert.IsType<S3DocumentTagStore>(result);
        }
    }

    public class CreateDocumentTagStoreDefault
    {
        [Fact]
        public void Should_return_s3_document_tag_store()
        {
            var sut = new S3TagFactory(
                Substitute.For<IS3ClientFactory>(),
                CreateTypeSettings(),
                CreateS3Settings());

            var result = sut.CreateDocumentTagStore();

            Assert.NotNull(result);
            Assert.IsType<S3DocumentTagStore>(result);
        }
    }

    public class CreateDocumentTagStoreByType
    {
        [Fact]
        public void Should_return_s3_document_tag_store()
        {
            var sut = new S3TagFactory(
                Substitute.For<IS3ClientFactory>(),
                CreateTypeSettings(),
                CreateS3Settings());

            var result = sut.CreateDocumentTagStore("s3");

            Assert.NotNull(result);
            Assert.IsType<S3DocumentTagStore>(result);
        }
    }

    public class CreateStreamTagStoreFromDocument
    {
        [Fact]
        public void Should_throw_when_document_is_null()
        {
            var sut = new S3TagFactory(
                Substitute.For<IS3ClientFactory>(),
                CreateTypeSettings(),
                CreateS3Settings());

            Assert.Throws<ArgumentNullException>(() => sut.CreateStreamTagStore((IObjectDocument)null!));
        }

        [Fact]
        public void Should_return_s3_stream_tag_store()
        {
            var doc = Substitute.For<IObjectDocument>();
            var streamInfo = new StreamInformation { EventStreamTagType = "s3" };
            doc.Active.Returns(streamInfo);

            var sut = new S3TagFactory(
                Substitute.For<IS3ClientFactory>(),
                CreateTypeSettings(),
                CreateS3Settings());

            var result = sut.CreateStreamTagStore(doc);

            Assert.NotNull(result);
            Assert.IsType<S3StreamTagStore>(result);
        }
    }

    public class CreateStreamTagStoreDefault
    {
        [Fact]
        public void Should_return_s3_stream_tag_store()
        {
            var sut = new S3TagFactory(
                Substitute.For<IS3ClientFactory>(),
                CreateTypeSettings(),
                CreateS3Settings());

            var result = sut.CreateStreamTagStore();

            Assert.NotNull(result);
            Assert.IsType<S3StreamTagStore>(result);
        }
    }
}
