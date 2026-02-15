using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3ObjectDocumentFactoryTests
{
    public class ConstructorWithDocumentStore
    {
        [Fact]
        public void Should_throw_when_document_store_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3ObjectDocumentFactory((IS3DocumentStore)null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_document_store()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);
            Assert.NotNull(sut);
        }
    }

    public class GetOrCreateAsync
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetOrCreateAsync(null!, "test-id"));
        }

        [Fact]
        public async Task Should_throw_when_object_id_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetOrCreateAsync("test", null!));
        }

        [Fact]
        public async Task Should_call_document_store_create()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var expected = Substitute.For<IObjectDocument>();
            docStore.CreateAsync("test", "123", null).Returns(expected);

            var sut = new S3ObjectDocumentFactory(docStore);
            var result = await sut.GetOrCreateAsync("Test", "123");

            Assert.Same(expected, result);
            await docStore.Received(1).CreateAsync("test", "123", null);
        }
    }

    public class GetAsync
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetAsync(null!, "test-id"));
        }

        [Fact]
        public async Task Should_throw_when_object_id_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetAsync("test", null!));
        }

        [Fact]
        public async Task Should_call_document_store_get()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var expected = Substitute.For<IObjectDocument>();
            docStore.GetAsync("test", "123", null).Returns(expected);

            var sut = new S3ObjectDocumentFactory(docStore);
            var result = await sut.GetAsync("Test", "123");

            Assert.Same(expected, result);
        }
    }

    public class SetAsync
    {
        [Fact]
        public async Task Should_throw_when_document_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SetAsync(null!));
        }

        [Fact]
        public async Task Should_call_document_store_set()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var document = Substitute.For<IObjectDocument>();

            var sut = new S3ObjectDocumentFactory(docStore);
            await sut.SetAsync(document);

            await docStore.Received(1).SetAsync(document);
        }
    }

    public class GetFirstByObjectDocumentTag
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetFirstByObjectDocumentTag(null!, "tag"));
        }

        [Fact]
        public async Task Should_throw_when_tag_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetFirstByObjectDocumentTag("test", null!));
        }
    }

    public class GetByObjectDocumentTag
    {
        [Fact]
        public async Task Should_throw_when_object_name_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetByObjectDocumentTag(null!, "tag"));
        }

        [Fact]
        public async Task Should_throw_when_tag_is_null()
        {
            var docStore = Substitute.For<IS3DocumentStore>();
            var sut = new S3ObjectDocumentFactory(docStore);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.GetByObjectDocumentTag("test", null!));
        }
    }
}
