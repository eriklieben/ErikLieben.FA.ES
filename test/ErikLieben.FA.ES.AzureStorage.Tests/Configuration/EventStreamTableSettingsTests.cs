using ErikLieben.FA.ES.AzureStorage.Configuration;
using Xunit;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Configuration;

public class EventStreamTableSettingsTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance_with_required_parameters()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.NotNull(sut);
            Assert.Equal("defaultStore", sut.DefaultDataStore);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_defaultDataStore_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new EventStreamTableSettings(null!));
        }

        [Fact]
        public void Should_use_defaultDataStore_for_DocumentStore_when_not_specified()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("defaultStore", sut.DefaultDocumentStore);
        }

        [Fact]
        public void Should_use_defaultDataStore_for_SnapShotStore_when_not_specified()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("defaultStore", sut.DefaultSnapShotStore);
        }

        [Fact]
        public void Should_use_defaultDataStore_for_DocumentTagStore_when_not_specified()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("defaultStore", sut.DefaultDocumentTagStore);
        }

        [Fact]
        public void Should_use_custom_DocumentStore_when_specified()
        {
            // Act
            var sut = new EventStreamTableSettings(
                "defaultStore",
                defaultDocumentStore: "customDocStore");

            // Assert
            Assert.Equal("customDocStore", sut.DefaultDocumentStore);
        }

        [Fact]
        public void Should_use_custom_SnapShotStore_when_specified()
        {
            // Act
            var sut = new EventStreamTableSettings(
                "defaultStore",
                defaultSnapShotStore: "customSnapStore");

            // Assert
            Assert.Equal("customSnapStore", sut.DefaultSnapShotStore);
        }

        [Fact]
        public void Should_use_custom_DocumentTagStore_when_specified()
        {
            // Act
            var sut = new EventStreamTableSettings(
                "defaultStore",
                defaultDocumentTagStore: "customTagStore");

            // Assert
            Assert.Equal("customTagStore", sut.DefaultDocumentTagStore);
        }
    }

    public class DefaultValues
    {
        [Fact]
        public void Should_have_AutoCreateTable_true_by_default()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.True(sut.AutoCreateTable);
        }

        [Fact]
        public void Should_have_EnableStreamChunks_false_by_default()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.False(sut.EnableStreamChunks);
        }

        [Fact]
        public void Should_have_DefaultChunkSize_1000_by_default()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal(1000, sut.DefaultChunkSize);
        }

        [Fact]
        public void Should_have_correct_default_DocumentTableName()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("objectdocumentstore", sut.DefaultDocumentTableName);
        }

        [Fact]
        public void Should_have_correct_default_EventTableName()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("eventstream", sut.DefaultEventTableName);
        }

        [Fact]
        public void Should_have_correct_default_SnapshotTableName()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("snapshots", sut.DefaultSnapshotTableName);
        }

        [Fact]
        public void Should_have_correct_default_DocumentTagTableName()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("documenttags", sut.DefaultDocumentTagTableName);
        }

        [Fact]
        public void Should_have_correct_default_StreamTagTableName()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("streamtags", sut.DefaultStreamTagTableName);
        }

        [Fact]
        public void Should_have_correct_default_StreamChunkTableName()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("streamchunks", sut.DefaultStreamChunkTableName);
        }

        [Fact]
        public void Should_have_correct_default_DocumentSnapShotTableName()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("documentsnapshots", sut.DefaultDocumentSnapShotTableName);
        }

        [Fact]
        public void Should_have_correct_default_TerminatedStreamTableName()
        {
            // Act
            var sut = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal("terminatedstreams", sut.DefaultTerminatedStreamTableName);
        }
    }

    public class CustomValues
    {
        [Fact]
        public void Should_set_AutoCreateTable_to_false_when_specified()
        {
            // Act
            var sut = new EventStreamTableSettings(
                "defaultStore",
                autoCreateTable: false);

            // Assert
            Assert.False(sut.AutoCreateTable);
        }

        [Fact]
        public void Should_set_EnableStreamChunks_to_true_when_specified()
        {
            // Act
            var sut = new EventStreamTableSettings(
                "defaultStore",
                enableStreamChunks: true);

            // Assert
            Assert.True(sut.EnableStreamChunks);
        }

        [Fact]
        public void Should_set_custom_DefaultChunkSize()
        {
            // Act
            var sut = new EventStreamTableSettings(
                "defaultStore",
                defaultChunkSize: 500);

            // Assert
            Assert.Equal(500, sut.DefaultChunkSize);
        }

        [Fact]
        public void Should_set_custom_table_names()
        {
            // Act
            var sut = new EventStreamTableSettings(
                "defaultStore",
                defaultDocumentTableName: "customdocs",
                defaultEventTableName: "customevents",
                defaultSnapshotTableName: "customsnaps",
                defaultDocumentTagTableName: "customdoctags",
                defaultStreamTagTableName: "customstreamtags",
                defaultStreamChunkTableName: "customchunks",
                defaultDocumentSnapShotTableName: "customdocsnaps",
                defaultTerminatedStreamTableName: "customterminated");

            // Assert
            Assert.Equal("customdocs", sut.DefaultDocumentTableName);
            Assert.Equal("customevents", sut.DefaultEventTableName);
            Assert.Equal("customsnaps", sut.DefaultSnapshotTableName);
            Assert.Equal("customdoctags", sut.DefaultDocumentTagTableName);
            Assert.Equal("customstreamtags", sut.DefaultStreamTagTableName);
            Assert.Equal("customchunks", sut.DefaultStreamChunkTableName);
            Assert.Equal("customdocsnaps", sut.DefaultDocumentSnapShotTableName);
            Assert.Equal("customterminated", sut.DefaultTerminatedStreamTableName);
        }
    }

    public class RecordEquality
    {
        [Fact]
        public void Should_be_equal_when_all_properties_match()
        {
            // Arrange
            var sut1 = new EventStreamTableSettings("defaultStore");
            var sut2 = new EventStreamTableSettings("defaultStore");

            // Assert
            Assert.Equal(sut1, sut2);
        }

        [Fact]
        public void Should_not_be_equal_when_properties_differ()
        {
            // Arrange
            var sut1 = new EventStreamTableSettings("store1");
            var sut2 = new EventStreamTableSettings("store2");

            // Assert
            Assert.NotEqual(sut1, sut2);
        }
    }
}
