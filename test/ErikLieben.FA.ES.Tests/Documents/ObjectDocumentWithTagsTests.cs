using System;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Documents;
using NSubstitute;
using Xunit;

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
            public void Should_initialize_properly_with_both_tag_stores()
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
                var streamTagStore = Substitute.For<IDocumentTagStore>();

                // Act
                var sut = new ObjectDocumentWithTags(document, documentTagStore, streamTagStore);

                // Assert
                Assert.Equal(document.ObjectId, sut.ObjectId);
                Assert.Equal(document.ObjectName, sut.ObjectName);
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

            [Fact]
            public void Should_allow_null_streamTagStore()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                document.ObjectId.Returns("test-id");
                document.ObjectName.Returns("test-name");
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.TerminatedStreams.Returns([]);

                var documentTagStore = Substitute.For<IDocumentTagStore>();

                // Act
                var sut = new ObjectDocumentWithTags(document, documentTagStore, null);

                // Assert
                Assert.NotNull(sut);
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
            public async Task Should_call_streamTagStore_SetAsync_when_tagType_is_StreamTag()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                document.ObjectId.Returns("test-id");
                document.ObjectName.Returns("test-name");
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.TerminatedStreams.Returns([]);
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var streamTagStore = Substitute.For<IDocumentTagStore>();
                var sut = new ObjectDocumentWithTags(document, documentTagStore, streamTagStore);
                var tag = "test-tag";

                // Act
                await sut.SetTagAsync(tag, TagTypes.StreamTag);

                // Assert
                await streamTagStore.Received(1).SetAsync(sut, tag);
                await documentTagStore.DidNotReceive().SetAsync(Arg.Any<IObjectDocument>(), Arg.Any<string>());
            }

            [Fact]
            public async Task Should_throw_InvalidOperationException_when_streamTagStore_is_null_and_tagType_is_StreamTag()
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
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await sut.SetTagAsync(tag, TagTypes.StreamTag));

                Assert.Contains("Stream tag store is not configured", exception.Message);
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

        public class RemoveTagAsync
        {
            [Fact]
            public async Task Should_call_documentTagStore_RemoveAsync_when_tagType_is_DocumentTag()
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
                await sut.RemoveTagAsync(tag, TagTypes.DocumentTag);

                // Assert
                await documentTagStore.Received(1).RemoveAsync(sut, tag);
            }

            [Fact]
            public async Task Should_call_streamTagStore_RemoveAsync_when_tagType_is_StreamTag()
            {
                // Arrange
                var document = Substitute.For<IObjectDocument>();
                document.ObjectId.Returns("test-id");
                document.ObjectName.Returns("test-name");
                document.Active.Returns(Substitute.For<StreamInformation>());
                document.TerminatedStreams.Returns([]);
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var streamTagStore = Substitute.For<IDocumentTagStore>();
                var sut = new ObjectDocumentWithTags(document, documentTagStore, streamTagStore);
                var tag = "test-tag";

                // Act
                await sut.RemoveTagAsync(tag, TagTypes.StreamTag);

                // Assert
                await streamTagStore.Received(1).RemoveAsync(sut, tag);
                await documentTagStore.DidNotReceive().RemoveAsync(Arg.Any<IObjectDocument>(), Arg.Any<string>());
            }

            [Fact]
            public async Task Should_throw_InvalidOperationException_when_streamTagStore_is_null_and_tagType_is_StreamTag()
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
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await sut.RemoveTagAsync(tag, TagTypes.StreamTag));

                Assert.Contains("Stream tag store is not configured", exception.Message);
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
                await sut.RemoveTagAsync(tag);

                // Assert
                await documentTagStore.Received(1).RemoveAsync(sut, tag);
            }
        }
    }
}
