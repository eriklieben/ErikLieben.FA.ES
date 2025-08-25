using ErikLieben.FA.ES.Documents;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Documents
{
    public class ObjectDocumentWithTagsTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_initialize_properly_with_valid_parameters()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                document.ObjectId.Returns("test-id");
                document.ObjectName.Returns("test-name");
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.TerminatedStreams.Returns([]);
                document.SchemaVersion.Returns("1.0");
                document.Hash.Returns("hash");
                document.PrevHash.Returns("prevHash");

                var documentTagStore = Substitute.For<IDocumentTagStore>();

                // Act
                var sut = new ObjectDocumentWithTags(document, documentTagStore);

                // Assert
                Assert.Equal(document.ObjectId, sut.ObjectId);
                Assert.Equal(document.ObjectName, sut.ObjectName);
                Assert.Equal(document.Active, sut.Active);
                Assert.Equal(document.TerminatedStreams, sut.TerminatedStreams);
                Assert.Equal(document.SchemaVersion, sut.SchemaVersion);
                Assert.Equal(document.Hash, sut.Hash);
                Assert.Equal(document.PrevHash, sut.PrevHash);
            }

            [Fact]
            public void Should_throw_ArgumentNullException_when_document_is_null()
            {
                // Arrange
                IObjectDocument document = null!;
                var documentTagStore = Substitute.For<IDocumentTagStore>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new ObjectDocumentWithTags(document, documentTagStore));
            }

            [Fact]
            public void Should_throw_ArgumentNullException_when_documentTagStore_is_null()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                IDocumentTagStore documentTagStore = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new ObjectDocumentWithTags(document, documentTagStore));
            }
        }

        public class SetTagAsync
        {
            [Fact]
            public async Task Should_call_documentTagStore_SetAsync_when_tagType_is_DocumentTag()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                document.ObjectId.Returns("test-id");
                document.ObjectName.Returns("test-name");
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.TerminatedStreams.Returns([]);
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var sut = new ObjectDocumentWithTags(document, documentTagStore);
                var tag = "test-tag";

                // Act
                await sut.SetTagAsync(tag, TagTypes.DocumentTag);

                // Assert
                await documentTagStore.Received(1).SetAsync(sut, tag);
            }

            [Fact]
            public async Task Should_throw_NotImplementedException_when_tagType_is_StreamTag()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                document.ObjectId.Returns("test-id");
                document.ObjectName.Returns("test-name");
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.TerminatedStreams.Returns([]);
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var sut = new ObjectDocumentWithTags(document, documentTagStore);
                var tag = "test-tag";

                // Act & Assert
                var exception = await Assert.ThrowsAsync<NotImplementedException>(
                    async () => await sut.SetTagAsync(tag, TagTypes.StreamTag));

                Assert.Equal("Not supported yet", exception.Message);
            }

            [Fact]
            public async Task Should_use_DocumentTag_as_default_tagType()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                document.ObjectId.Returns("test-id");
                document.ObjectName.Returns("test-name");
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.TerminatedStreams.Returns([]);
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var sut = new ObjectDocumentWithTags(document, documentTagStore);
                var tag = "test-tag";

                // Act
                await sut.SetTagAsync(tag);

                // Assert
                await documentTagStore.Received(1).SetAsync(sut, tag);
            }
        }
    }
}
