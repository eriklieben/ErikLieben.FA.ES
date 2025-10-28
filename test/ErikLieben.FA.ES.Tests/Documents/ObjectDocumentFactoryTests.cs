using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Documents
{
    public class ObjectDocumentFactoryTests
    {
        public class Ctor
        {
            [Fact]
            public void Should_initialize_correctly_with_valid_parameters()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings
                {
                    DocumentType = "test",
                    DocumentTagType = "test"
                };

                // Act
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Assert
                Assert.NotNull(sut);
            }

            [Fact]
            public void Should_throw_when_objectDocumentFactories_is_null()
            {
                // Arrange
                IDictionary<string, IObjectDocumentFactory>? objectDocumentFactories = null;
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new ObjectDocumentFactory(
                    objectDocumentFactories!,
                    documentTagDocumentFactory,
                    settings));
            }

            [Fact]
            public void Should_throw_when_documentTagDocumentFactory_is_null()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                IDocumentTagDocumentFactory? documentTagDocumentFactory = null;
                var settings = new EventStreamDefaultTypeSettings();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory!,
                    settings));
            }

            [Fact]
            public void Should_throw_when_settings_is_null()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                EventStreamDefaultTypeSettings? settings = null;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings!));
            }
        }

        public class GetAsync
        {
            [Fact]
            public async Task Should_return_object_document_when_factory_found()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var expectedDocument = Substitute.For<IObjectDocument>();
                mockFactory.GetAsync("TestObject", "123").Returns(Task.FromResult(expectedDocument));

                objectDocumentFactories.Add("teststore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetAsync("TestObject", "123");

                // Assert
                Assert.Same(expectedDocument, result);
                await mockFactory.Received(1).GetAsync("TestObject", "123");
            }

            [Fact]
            public async Task Should_use_default_store_when_store_parameter_is_null()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var expectedDocument = Substitute.For<IObjectDocument>();
                mockFactory.GetAsync("TestObject", "123").Returns(Task.FromResult(expectedDocument));

                objectDocumentFactories.Add("teststore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetAsync("TestObject", "123", null);

                // Assert
                Assert.Same(expectedDocument, result);
                await mockFactory.Received(1).GetAsync("TestObject", "123");
            }

            [Fact]
            public async Task Should_use_provided_store_when_not_null()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "DefaultStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var expectedDocument = Substitute.For<IObjectDocument>();
                mockFactory.GetAsync("TestObject", "123", "CustomStore").Returns(Task.FromResult(expectedDocument));

                objectDocumentFactories.Add("defaultstore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetAsync("TestObject", "123", "CustomStore");

                // Assert
                Assert.Same(expectedDocument, result);
                await mockFactory.Received(1).GetAsync("TestObject", "123", "CustomStore");
            }

            [Fact]
            public async Task Should_throw_when_objectName_is_null()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.GetAsync(null!, "123"));
            }

            [Fact]
            public async Task Should_throw_when_objectName_is_empty()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(async () => await sut.GetAsync("", "123"));
            }

            [Fact]
            public async Task Should_throw_when_objectId_is_null()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.GetAsync("TestObject", null!));
            }

            [Fact]
            public async Task Should_throw_when_objectId_is_empty()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(async () => await sut.GetAsync("TestObject", ""));
            }

            [Fact]
            public async Task Should_throw_when_factory_not_found()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<UnableToFindDocumentFactoryException>(async () =>
                    await sut.GetAsync("TestObject", "123"));
            }
        }

        public class GetOrCreateAsync
        {
            [Fact]
            public async Task Should_return_object_document_when_factory_found()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var expectedDocument = Substitute.For<IObjectDocument>();
                mockFactory.GetOrCreateAsync("TestObject", "123").Returns(Task.FromResult(expectedDocument));

                objectDocumentFactories.Add("teststore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetOrCreateAsync("TestObject", "123");

                // Assert
                Assert.Same(expectedDocument, result);
                await mockFactory.Received(1).GetOrCreateAsync("TestObject", "123");
            }

            [Fact]
            public async Task Should_use_default_store_when_store_parameter_is_null()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var expectedDocument = Substitute.For<IObjectDocument>();
                mockFactory.GetOrCreateAsync("TestObject", "123").Returns(Task.FromResult(expectedDocument));

                objectDocumentFactories.Add("teststore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetOrCreateAsync("TestObject", "123", null);

                // Assert
                Assert.Same(expectedDocument, result);
                await mockFactory.Received(1).GetOrCreateAsync("TestObject", "123");
            }

            [Fact]
            public async Task Should_use_provided_store_when_not_null()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "DefaultStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var expectedDocument = Substitute.For<IObjectDocument>();
                mockFactory.GetOrCreateAsync("TestObject", "123", "CustomStore").Returns(Task.FromResult(expectedDocument));

                objectDocumentFactories.Add("defaultstore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetOrCreateAsync("TestObject", "123", "CustomStore");

                // Assert
                Assert.Same(expectedDocument, result);
                await mockFactory.Received(1).GetOrCreateAsync("TestObject", "123", "CustomStore");
            }

            [Fact]
            public async Task Should_throw_when_objectName_is_null()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.GetOrCreateAsync(null!, "123"));
            }

            [Fact]
            public async Task Should_throw_when_objectName_is_empty()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(async () => await sut.GetOrCreateAsync("", "123"));
            }

            [Fact]
            public async Task Should_throw_when_objectId_is_null()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.GetOrCreateAsync("TestObject", null!));
            }

            [Fact]
            public async Task Should_throw_when_objectId_is_empty()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(async () => await sut.GetOrCreateAsync("TestObject", ""));
            }

            [Fact]
            public async Task Should_throw_when_factory_not_found()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<UnableToFindDocumentFactoryException>(async () =>
                    await sut.GetOrCreateAsync("TestObject", "123"));
            }
        }

        public class GetFirstByObjectDocumentTag
        {
            [Fact]
            public async Task Should_return_null_when_no_object_ids_found()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "TestTagStore" };

                var documentTagStore = Substitute.For<IDocumentTagStore>();
                documentTagStore.GetAsync("TestObject", "TestTag").Returns(Task.FromResult<IEnumerable<string>>(new List<string>()));

                documentTagDocumentFactory
                    .CreateDocumentTagStore(settings.DocumentTagType)
                    .Returns(documentTagStore);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetFirstByObjectDocumentTag("TestObject", "TestTag");

                // Assert
                Assert.Null(result);
                await documentTagStore.Received(1).GetAsync("TestObject", "TestTag");
            }

            [Fact]
            public async Task Should_return_null_when_first_object_id_is_empty()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "TestTagStore" };

                var documentTagStore = Substitute.For<IDocumentTagStore>();
                documentTagStore.GetAsync("TestObject", "TestTag").Returns(Task.FromResult<IEnumerable<string>>(new List<string> { "" }));

                documentTagDocumentFactory
                    .CreateDocumentTagStore(settings.DocumentTagType)
                    .Returns(documentTagStore);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetFirstByObjectDocumentTag("TestObject", "TestTag");

                // Assert
                Assert.Null(result);
                await documentTagStore.Received(1).GetAsync("TestObject", "TestTag");
            }

            [Fact]
            public async Task Should_return_document_when_valid_object_id_found()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings
                {
                    DocumentType = "TestStore",
                    DocumentTagType = "TestTagStore"
                };

                var documentTagStore = Substitute.For<IDocumentTagStore>();
                documentTagStore.GetAsync("TestObject", "TestTag").Returns(Task.FromResult<IEnumerable<string>>(new List<string> { "123" }));

                documentTagDocumentFactory
                    .CreateDocumentTagStore(settings.DocumentTagType)
                    .Returns(documentTagStore);

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var expectedDocument = Substitute.For<IObjectDocument>();
                mockFactory.GetAsync("TestObject", "123").Returns(Task.FromResult(expectedDocument));

                objectDocumentFactories.Add("teststore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetFirstByObjectDocumentTag("TestObject", "TestTag");

                // Assert
                Assert.Same(expectedDocument, result);
                await documentTagStore.Received(1).GetAsync("TestObject", "TestTag");
                await mockFactory.Received(1).GetAsync("TestObject", "123");
            }
        }

        public class GetByObjectDocumentTag
        {
            [Fact]
            public async Task Should_return_empty_list_when_no_object_ids_found()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentTagType = "TestTagStore" };

                var documentTagStore = Substitute.For<IDocumentTagStore>();
                documentTagStore.GetAsync("TestObject", "TestTag").Returns(Task.FromResult<IEnumerable<string>>(new List<string>()));

                documentTagDocumentFactory
                    .CreateDocumentTagStore(settings.DocumentTagType)
                    .Returns(documentTagStore);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetByObjectDocumentTag("TestObject", "TestTag");

                // Assert
                Assert.Empty(result);
                await documentTagStore.Received(1).GetAsync("TestObject", "TestTag");
            }

            [Fact]
            public async Task Should_skip_empty_object_ids()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings
                {
                    DocumentType = "TestStore",
                    DocumentTagType = "TestTagStore"
                };

                var documentTagStore = Substitute.For<IDocumentTagStore>();
                documentTagStore.GetAsync("TestObject", "TestTag")
                    .Returns(Task.FromResult<IEnumerable<string>>(new List<string> { "", "123", null!, "456", " " }));

                documentTagDocumentFactory
                    .CreateDocumentTagStore(settings.DocumentTagType)
                    .Returns(documentTagStore);

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var document1 = Substitute.For<IObjectDocument>();
                var document2 = Substitute.For<IObjectDocument>();
                mockFactory.GetAsync("TestObject", "123").Returns(Task.FromResult(document1));
                mockFactory.GetAsync("TestObject", "456").Returns(Task.FromResult(document2));

                objectDocumentFactories.Add("teststore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                var result = await sut.GetByObjectDocumentTag("TestObject", "TestTag");

                // Assert
                var resultList = result.ToList();
                Assert.Equal(2, resultList.Count);
                Assert.Contains(document1, resultList);
                Assert.Contains(document2, resultList);
                await documentTagStore.Received(1).GetAsync("TestObject", "TestTag");
                await mockFactory.Received(1).GetAsync("TestObject", "123");
                await mockFactory.Received(1).GetAsync("TestObject", "456");
            }
        }

        public class SetAsync
        {
            [Fact]
            public async Task Should_set_document_when_factory_found()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var document = Substitute.For<IObjectDocument>();

                objectDocumentFactories.Add("teststore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                await sut.SetAsync(document);

                // Assert
                await mockFactory.Received(1).SetAsync(document);
            }

            [Fact]
            public async Task Should_use_default_store_when_store_parameter_is_null()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var document = Substitute.For<IObjectDocument>();

                objectDocumentFactories.Add("teststore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                await sut.SetAsync(document, null);

                // Assert
                await mockFactory.Received(1).SetAsync(document);
            }

            [Fact]
            public async Task Should_use_provided_store_when_not_null()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "DefaultStore" };

                var mockFactory = Substitute.For<IObjectDocumentFactory>();
                var document = Substitute.For<IObjectDocument>();

                objectDocumentFactories.Add("defaultstore", mockFactory);

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                await sut.SetAsync(document, "CustomStore");

                // Assert
                await mockFactory.Received(1).SetAsync(document, "CustomStore");
            }

            [Fact]
            public async Task Should_throw_when_document_is_null()
            {
                // Arrange
                var objectDocumentFactories = Substitute.For<IDictionary<string, IObjectDocumentFactory>>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings();
                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.SetAsync(null!));
            }

            [Fact]
            public async Task Should_not_throw_when_factory_not_found()
            {
                // Arrange
                var objectDocumentFactories = new Dictionary<string, IObjectDocumentFactory>();
                var documentTagDocumentFactory = Substitute.For<IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings { DocumentType = "TestStore" };
                var document = Substitute.For<IObjectDocument>();

                var sut = new ObjectDocumentFactory(
                    objectDocumentFactories,
                    documentTagDocumentFactory,
                    settings);

                // Act
                await sut.SetAsync(document);

                // Assert
                Assert.True(true);
            }
        }
    }
}
