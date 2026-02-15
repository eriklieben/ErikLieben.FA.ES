using Amazon.S3;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3EventStreamFactoryTests
{
    private static EventStreamS3Settings CreateSettings() =>
        new("s3", serviceUrl: "http://localhost:9000", accessKey: "key", secretKey: "secret");

    public class Constructor
    {
        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new S3EventStreamFactory(
                null!,
                Substitute.For<IS3ClientFactory>(),
                Substitute.For<IDocumentTagDocumentFactory>(),
                Substitute.For<IObjectDocumentFactory>(),
                Substitute.For<IAggregateFactory>()));
        }

        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new S3EventStreamFactory(
                CreateSettings(),
                null!,
                Substitute.For<IDocumentTagDocumentFactory>(),
                Substitute.For<IObjectDocumentFactory>(),
                Substitute.For<IAggregateFactory>()));
        }

        [Fact]
        public void Should_throw_when_document_tag_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new S3EventStreamFactory(
                CreateSettings(),
                Substitute.For<IS3ClientFactory>(),
                null!,
                Substitute.For<IObjectDocumentFactory>(),
                Substitute.For<IAggregateFactory>()));
        }

        [Fact]
        public void Should_throw_when_object_document_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new S3EventStreamFactory(
                CreateSettings(),
                Substitute.For<IS3ClientFactory>(),
                Substitute.For<IDocumentTagDocumentFactory>(),
                null!,
                Substitute.For<IAggregateFactory>()));
        }

        [Fact]
        public void Should_throw_when_aggregate_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new S3EventStreamFactory(
                CreateSettings(),
                Substitute.For<IS3ClientFactory>(),
                Substitute.For<IDocumentTagDocumentFactory>(),
                Substitute.For<IObjectDocumentFactory>(),
                null!));
        }
    }

    public class Create
    {
        [Fact]
        public void Should_return_s3_event_stream()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            var docTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
            var tagStore = Substitute.For<IDocumentTagStore>();
            docTagFactory.CreateDocumentTagStore(Arg.Any<IObjectDocument>()).Returns(tagStore);
            docTagFactory.CreateStreamTagStore(Arg.Any<IObjectDocument>()).Returns(tagStore);

            var sut = new S3EventStreamFactory(
                CreateSettings(),
                clientFactory,
                docTagFactory,
                Substitute.For<IObjectDocumentFactory>(),
                Substitute.For<IAggregateFactory>());

            var streamInfo = new StreamInformation
            {
                StreamType = "s3",
                DocumentTagType = "s3",
                EventStreamTagType = "s3",
                StreamIdentifier = "test-0000000000"
            };

            var document = Substitute.For<IObjectDocument>();
            document.Active.Returns(streamInfo);
            document.ObjectName.Returns("test");
            document.ObjectId.Returns("123");
            document.TerminatedStreams.Returns(new List<TerminatedStream>());

            var stream = sut.Create(document);

            Assert.NotNull(stream);
            Assert.IsType<S3EventStream>(stream);
        }

        [Fact]
        public void Should_set_default_stream_type_when_default()
        {
            var clientFactory = Substitute.For<IS3ClientFactory>();
            var s3Client = Substitute.For<IAmazonS3>();
            clientFactory.CreateClient(Arg.Any<string>()).Returns(s3Client);

            var docTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
            var tagStore = Substitute.For<IDocumentTagStore>();
            docTagFactory.CreateDocumentTagStore(Arg.Any<IObjectDocument>()).Returns(tagStore);
            docTagFactory.CreateStreamTagStore(Arg.Any<IObjectDocument>()).Returns(tagStore);

            var settings = CreateSettings();
            var sut = new S3EventStreamFactory(
                settings,
                clientFactory,
                docTagFactory,
                Substitute.For<IObjectDocumentFactory>(),
                Substitute.For<IAggregateFactory>());

            var streamInfo = new StreamInformation
            {
                StreamType = "default",
                DocumentTagType = "s3",
                EventStreamTagType = "s3",
                StreamIdentifier = "test-0000000000"
            };

            var document = Substitute.For<IObjectDocument>();
            document.Active.Returns(streamInfo);
            document.ObjectName.Returns("test");
            document.ObjectId.Returns("123");
            document.TerminatedStreams.Returns(new List<TerminatedStream>());

            sut.Create(document);

            Assert.Equal(settings.DefaultDataStore, streamInfo.StreamType);
        }
    }
}
