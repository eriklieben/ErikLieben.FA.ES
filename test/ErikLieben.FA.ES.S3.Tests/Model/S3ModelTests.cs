using ErikLieben.FA.ES.S3.Model;

namespace ErikLieben.FA.ES.S3.Tests.Model;

public class S3ModelTests
{
    public class S3DataStoreDocumentTests
    {
        [Fact]
        public void Should_have_default_events_list()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test",
                ObjectName = "test",
                LastObjectDocumentHash = "*"
            };
            Assert.NotNull(doc.Events);
            Assert.Empty(doc.Events);
        }

        [Fact]
        public void Should_set_object_id()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test-id",
                ObjectName = "test",
                LastObjectDocumentHash = "*"
            };
            Assert.Equal("test-id", doc.ObjectId);
        }

        [Fact]
        public void Should_set_object_name()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test",
                ObjectName = "test-name",
                LastObjectDocumentHash = "*"
            };
            Assert.Equal("test-name", doc.ObjectName);
        }

        [Fact]
        public void Should_default_terminated_to_false()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test",
                ObjectName = "test",
                LastObjectDocumentHash = "*"
            };
            Assert.False(doc.Terminated);
        }

        [Fact]
        public void Should_default_schema_version()
        {
            var doc = new S3DataStoreDocument
            {
                ObjectId = "test",
                ObjectName = "test",
                LastObjectDocumentHash = "*"
            };
            Assert.Equal("1.0.0", doc.SchemaVersion);
        }
    }

    public class S3DocumentTagStoreDocumentTests
    {
        [Fact]
        public void Should_have_default_object_ids_list()
        {
            var doc = new S3DocumentTagStoreDocument { Tag = "test" };
            Assert.NotNull(doc.ObjectIds);
            Assert.Empty(doc.ObjectIds);
        }

        [Fact]
        public void Should_set_tag()
        {
            var doc = new S3DocumentTagStoreDocument { Tag = "my-tag" };
            Assert.Equal("my-tag", doc.Tag);
        }

        [Fact]
        public void Should_default_schema_version()
        {
            var doc = new S3DocumentTagStoreDocument { Tag = "test" };
            Assert.Equal("1.0.0", doc.SchemaVersion);
        }
    }
}
