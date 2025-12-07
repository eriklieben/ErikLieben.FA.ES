using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbObjectDocumentFactoryTests
{
    private readonly ICosmosDbDocumentStore cosmosDbDocumentStore;
    private readonly CosmosClient cosmosClient;
    private readonly IDocumentTagDocumentFactory documentTagStore;
    private readonly EventStreamDefaultTypeSettings settings;
    private readonly EventStreamCosmosDbSettings cosmosDbSettings;

    public CosmosDbObjectDocumentFactoryTests()
    {
        cosmosDbDocumentStore = Substitute.For<ICosmosDbDocumentStore>();
        cosmosClient = Substitute.For<CosmosClient>();
        documentTagStore = Substitute.For<IDocumentTagDocumentFactory>();
        settings = new EventStreamDefaultTypeSettings();
        cosmosDbSettings = new EventStreamCosmosDbSettings();
    }

    public class ConstructorWithDocumentStore : CosmosDbObjectDocumentFactoryTests
    {
        [Fact]
        public void Should_throw_argument_null_exception_when_document_store_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new CosmosDbObjectDocumentFactory((ICosmosDbDocumentStore)null!));
        }

        [Fact]
        public void Should_create_instance_when_document_store_is_valid()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            Assert.NotNull(sut);
        }
    }

    public class ConstructorWithDependencies : CosmosDbObjectDocumentFactoryTests
    {
        [Fact]
        public void Should_create_instance_when_all_parameters_are_valid()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosClient, documentTagStore, settings, cosmosDbSettings);
            Assert.NotNull(sut);
        }
    }

    public class GetOrCreateAsync : CosmosDbObjectDocumentFactoryTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetOrCreateAsync(null!, "test-id"));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_object_name_is_empty()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentException>(() => sut.GetOrCreateAsync("", "test-id"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetOrCreateAsync("TestObject", null!));
        }

        [Fact]
        public async Task Should_throw_argument_exception_when_object_id_is_empty()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentException>(() => sut.GetOrCreateAsync("TestObject", ""));
        }

        [Fact]
        public async Task Should_throw_invalid_operation_exception_when_document_store_returns_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            cosmosDbDocumentStore.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns((IObjectDocument?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetOrCreateAsync("TestObject", "test-id"));
        }

        [Fact]
        public async Task Should_return_document_from_document_store()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            var document = Substitute.For<IObjectDocument>();
            document.Active.Returns(new StreamInformation { StreamIdentifier = "stream-123" });

            cosmosDbDocumentStore.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(document);

            var result = await sut.GetOrCreateAsync("TestObject", "test-id");

            Assert.NotNull(result);
            Assert.Same(document, result);
        }

        [Fact]
        public async Task Should_use_lowercase_object_name()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            var document = Substitute.For<IObjectDocument>();
            document.Active.Returns(new StreamInformation { StreamIdentifier = "stream-123" });

            cosmosDbDocumentStore.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(document);

            await sut.GetOrCreateAsync("TestObject", "test-id");

            await cosmosDbDocumentStore.Received(1).CreateAsync("testobject", "test-id", null);
        }
    }

    public class GetAsync : CosmosDbObjectDocumentFactoryTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetAsync(null!, "test-id"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_id_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetAsync("TestObject", null!));
        }

        [Fact]
        public async Task Should_throw_invalid_operation_exception_when_document_store_returns_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            cosmosDbDocumentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns((IObjectDocument?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync("TestObject", "test-id"));
        }

        [Fact]
        public async Task Should_return_document_from_document_store()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            var document = Substitute.For<IObjectDocument>();

            cosmosDbDocumentStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(document);

            var result = await sut.GetAsync("TestObject", "test-id");

            Assert.Same(document, result);
        }
    }

    public class GetFirstByObjectDocumentTag : CosmosDbObjectDocumentFactoryTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetFirstByObjectDocumentTag(null!, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_tag_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetFirstByObjectDocumentTag("TestObject", null!));
        }

        [Fact]
        public async Task Should_return_null_when_no_document_found()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            cosmosDbDocumentStore.GetFirstByDocumentByTagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
                .Returns((IObjectDocument?)null);

            var result = await sut.GetFirstByObjectDocumentTag("TestObject", "test-tag");

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_document_when_found()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            var document = Substitute.For<IObjectDocument>();

            cosmosDbDocumentStore.GetFirstByDocumentByTagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
                .Returns(document);

            var result = await sut.GetFirstByObjectDocumentTag("TestObject", "test-tag");

            Assert.Same(document, result);
        }
    }

    public class GetByObjectDocumentTag : CosmosDbObjectDocumentFactoryTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_object_name_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetByObjectDocumentTag(null!, "test-tag"));
        }

        [Fact]
        public async Task Should_throw_argument_null_exception_when_tag_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetByObjectDocumentTag("TestObject", null!));
        }

        [Fact]
        public async Task Should_return_empty_when_no_documents_found()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            cosmosDbDocumentStore.GetByDocumentByTagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
                .Returns((IEnumerable<IObjectDocument>?)null);

            var result = await sut.GetByObjectDocumentTag("TestObject", "test-tag");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_documents_when_found()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            var documents = new[] { Substitute.For<IObjectDocument>(), Substitute.For<IObjectDocument>() };

            cosmosDbDocumentStore.GetByDocumentByTagAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
                .Returns(documents);

            var result = await sut.GetByObjectDocumentTag("TestObject", "test-tag");

            Assert.Equal(2, result.Count());
        }
    }

    public class SetAsync : CosmosDbObjectDocumentFactoryTests
    {
        [Fact]
        public async Task Should_throw_argument_null_exception_when_document_is_null()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetAsync(null!));
        }

        [Fact]
        public async Task Should_call_document_store_set_async()
        {
            var sut = new CosmosDbObjectDocumentFactory(cosmosDbDocumentStore);
            var document = Substitute.For<IObjectDocument>();

            await sut.SetAsync(document);

            await cosmosDbDocumentStore.Received(1).SetAsync(document);
        }
    }
}
