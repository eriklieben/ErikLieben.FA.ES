using ErikLieben.FA.ES.AzureStorage.Configuration;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Configuration;

public class EventStreamBlobSettingsTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance_with_required_parameters()
        {
            // Act
            var sut = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.NotNull(sut);
            Assert.Equal("defaultStore", sut.DefaultDataStore);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_defaultDataStore_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventStreamBlobSettings(null!));
        }

        [Fact]
        public void Should_use_defaultDataStore_for_DocumentStore_when_not_specified()
        {
            // Act
            var sut = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.Equal("defaultStore", sut.DefaultDocumentStore);
        }

        [Fact]
        public void Should_use_defaultDataStore_for_SnapShotStore_when_not_specified()
        {
            // Act
            var sut = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.Equal("defaultStore", sut.DefaultSnapShotStore);
        }

        [Fact]
        public void Should_use_defaultDataStore_for_DocumentTagStore_when_not_specified()
        {
            // Act
            var sut = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.Equal("defaultStore", sut.DefaultDocumentTagStore);
        }

        [Fact]
        public void Should_use_custom_DocumentStore_when_specified()
        {
            // Act
            var sut = new EventStreamBlobSettings(
                "defaultStore",
                defaultDocumentStore: "customDocStore");

            // Assert
            Assert.Equal("customDocStore", sut.DefaultDocumentStore);
        }

        [Fact]
        public void Should_use_custom_SnapShotStore_when_specified()
        {
            // Act
            var sut = new EventStreamBlobSettings(
                "defaultStore",
                defaultSnapShotStore: "customSnapStore");

            // Assert
            Assert.Equal("customSnapStore", sut.DefaultSnapShotStore);
        }

        [Fact]
        public void Should_use_custom_DocumentTagStore_when_specified()
        {
            // Act
            var sut = new EventStreamBlobSettings(
                "defaultStore",
                defaultDocumentTagStore: "customTagStore");

            // Assert
            Assert.Equal("customTagStore", sut.DefaultDocumentTagStore);
        }
    }

    public class DefaultValues
    {
        [Fact]
        public void Should_have_AutoCreateContainer_true_by_default()
        {
            // Act
            var sut = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.True(sut.AutoCreateContainer);
        }

        [Fact]
        public void Should_have_EnableStreamChunks_false_by_default()
        {
            // Act
            var sut = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.False(sut.EnableStreamChunks);
        }

        [Fact]
        public void Should_have_DefaultChunkSize_1000_by_default()
        {
            // Act
            var sut = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.Equal(1000, sut.DefaultChunkSize);
        }

        [Fact]
        public void Should_have_correct_default_DocumentContainerName()
        {
            // Act
            var sut = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.Equal("object-document-store", sut.DefaultDocumentContainerName);
        }
    }

    public class CustomValues
    {
        [Fact]
        public void Should_set_AutoCreateContainer_to_false_when_specified()
        {
            // Act
            var sut = new EventStreamBlobSettings(
                "defaultStore",
                autoCreateContainer: false);

            // Assert
            Assert.False(sut.AutoCreateContainer);
        }

        [Fact]
        public void Should_set_EnableStreamChunks_to_true_when_specified()
        {
            // Act
            var sut = new EventStreamBlobSettings(
                "defaultStore",
                enableStreamChunks: true);

            // Assert
            Assert.True(sut.EnableStreamChunks);
        }

        [Fact]
        public void Should_set_custom_DefaultChunkSize()
        {
            // Act
            var sut = new EventStreamBlobSettings(
                "defaultStore",
                defaultChunkSize: 500);

            // Assert
            Assert.Equal(500, sut.DefaultChunkSize);
        }

        [Fact]
        public void Should_set_custom_DefaultDocumentContainerName()
        {
            // Act
            var sut = new EventStreamBlobSettings(
                "defaultStore",
                defaultDocumentContainerName: "custom-container");

            // Assert
            Assert.Equal("custom-container", sut.DefaultDocumentContainerName);
        }
    }

    public class RecordEquality
    {
        [Fact]
        public void Should_be_equal_when_all_properties_match()
        {
            // Arrange
            var sut1 = new EventStreamBlobSettings("defaultStore");
            var sut2 = new EventStreamBlobSettings("defaultStore");

            // Assert
            Assert.Equal(sut1, sut2);
        }

        [Fact]
        public void Should_not_be_equal_when_properties_differ()
        {
            // Arrange
            var sut1 = new EventStreamBlobSettings("store1");
            var sut2 = new EventStreamBlobSettings("store2");

            // Assert
            Assert.NotEqual(sut1, sut2);
        }
    }
}
