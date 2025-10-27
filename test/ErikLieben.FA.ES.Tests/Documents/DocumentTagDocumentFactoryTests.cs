using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Documents
{
    public class DocumentTagDocumentFactoryTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_throw_argument_null_exception_when_factories_is_null()
            {
                // Arrange
                IDictionary<string, IDocumentTagDocumentFactory> factories = null!;
                var settings = Substitute.For<EventStreamDefaultTypeSettings>();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new DocumentTagDocumentFactory(factories, settings));
            }

            [Fact]
            public void Should_throw_argument_null_exception_when_settings_is_null()
            {
                // Arrange
                var factories = new Dictionary<string, IDocumentTagDocumentFactory>();
                EventStreamDefaultTypeSettings settings = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new DocumentTagDocumentFactory(factories, settings));
            }

            [Fact]
            public void Should_not_throw_when_parameters_are_valid()
            {
                // Arrange
                var factories = new Dictionary<string, IDocumentTagDocumentFactory>();
                var settings = Substitute.For<EventStreamDefaultTypeSettings>();

                // Act
                var sut = new DocumentTagDocumentFactory(factories, settings);

                // Assert
                Assert.NotNull(sut);
            }
        }

        public class CreateDocumentTagStoreWithDocument
        {
            [Fact]
            public void Should_throw_argument_null_exception_when_document_is_null()
            {
                // Arrange
                var factories = new Dictionary<string, IDocumentTagDocumentFactory>();
                var settings = Substitute.For<EventStreamDefaultTypeSettings>();
                var sut = new DocumentTagDocumentFactory(factories, settings);
                IObjectDocument document = null!;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.CreateDocumentTagStore(document));
            }

            [Fact]
            public void Should_throw_argument_null_exception_when_document_tag_type_is_null()
            {
                // Arrange
                var factories = new Dictionary<string, IDocumentTagDocumentFactory>();
                var settings = Substitute.For<EventStreamDefaultTypeSettings>();
                var sut = new DocumentTagDocumentFactory(factories, settings);

                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                active.DocumentTagType = null!;
                document.Active.Returns(active);

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.CreateDocumentTagStore(document));
            }

            [Fact]
            public void Should_use_document_factory_when_document_tag_type_is_found()
            {
                // Arrange
                const string documentTagType = "blobTag";
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                documentTagFactory.CreateDocumentTagStore(Arg.Any<IObjectDocument>()).Returns(documentTagStore);

                var factories = new Dictionary<string, IDocumentTagDocumentFactory>
                {
                    { documentTagType, documentTagFactory }
                };

                var settings = Substitute.For<EventStreamDefaultTypeSettings>();
                var sut = new DocumentTagDocumentFactory(factories, settings);

                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                active.DocumentTagType = documentTagType;
                document.Active.Returns(active);

                // Act
                var result = sut.CreateDocumentTagStore(document);

                // Assert
                Assert.Same(documentTagStore, result);
                documentTagFactory.Received(1).CreateDocumentTagStore(document);
            }

            [Fact]
            public void Should_use_default_factory_when_document_tag_type_not_found_but_default_exists()
            {
                // Arrange
                const string documentTagType = "blobTag";
                const string defaultTagType = "defaultTag";
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var defaultTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                defaultTagFactory.CreateDocumentTagStore(Arg.Any<IObjectDocument>()).Returns(documentTagStore);

                var factories = new Dictionary<string, IDocumentTagDocumentFactory>
                {
                    { defaultTagType, defaultTagFactory }
                };

                var settings = new EventStreamDefaultTypeSettings
                {
                    DocumentTagType = defaultTagType
                };

                var sut = new DocumentTagDocumentFactory(factories, settings);

                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                active.DocumentTagType = documentTagType;
                document.Active.Returns(active);

                // Act
                var result = sut.CreateDocumentTagStore(document);

                // Assert
                Assert.Same(documentTagStore, result);
                defaultTagFactory.Received(1).CreateDocumentTagStore(document);
            }

            [Fact]
            public void Should_throw_exception_when_no_factory_found()
            {
                // Arrange
                const string documentTagType = "unknownTag";
                const string defaultTagType = "defaultTag";

                var factories = new Dictionary<string, IDocumentTagDocumentFactory>();
                var settings = new EventStreamDefaultTypeSettings
                {
                    DocumentTagType = defaultTagType
                };

                var sut = new DocumentTagDocumentFactory(factories, settings);

                var document = Substitute.For<IObjectDocument>();
                var active = Substitute.For<StreamInformation>();
                active.DocumentTagType = documentTagType;
                document.Active.Returns(active);

                // Act & Assert
                var exception = Assert.Throws<UnableToFindDocumentTagFactoryException>(
                    () => sut.CreateDocumentTagStore(document));

                Assert.Contains(documentTagType, exception.Message);
            }
        }

        public class CreateDocumentTagStoreWithType
        {
            [Fact]
            public void Should_use_factory_when_type_is_found()
            {
                // Arrange
                const string documentTagType = "blobTag";
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var documentTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                documentTagFactory.CreateDocumentTagStore(documentTagType).Returns(documentTagStore);

                var factories = new Dictionary<string, IDocumentTagDocumentFactory>
                {
                    { documentTagType, documentTagFactory }
                };

                var settings = Substitute.For<EventStreamDefaultTypeSettings>();
                var sut = new DocumentTagDocumentFactory(factories, settings);

                // Act
                var result = sut.CreateDocumentTagStore(documentTagType);

                // Assert
                Assert.Same(documentTagStore, result);
                documentTagFactory.Received(1).CreateDocumentTagStore(documentTagType);
            }

            [Fact]
            public void Should_use_default_factory_when_type_not_found_but_default_exists()
            {
                // Arrange
                const string documentTagType = "unknownTag";
                const string defaultTagType = "defaultTag";
                var documentTagStore = Substitute.For<IDocumentTagStore>();
                var defaultTagFactory = Substitute.For<IDocumentTagDocumentFactory>();
                defaultTagFactory.CreateDocumentTagStore(documentTagType).Returns(documentTagStore);

                var factories = new Dictionary<string, IDocumentTagDocumentFactory>
                {
                    { defaultTagType, defaultTagFactory }
                };

                var settings = new EventStreamDefaultTypeSettings
                {
                    DocumentTagType = defaultTagType
                };

                var sut = new DocumentTagDocumentFactory(factories, settings);

                // Act
                var result = sut.CreateDocumentTagStore(documentTagType);

                // Assert
                Assert.Same(documentTagStore, result);
                defaultTagFactory.Received(1).CreateDocumentTagStore(documentTagType);
            }

            [Fact]
            public void Should_throw_exception_when_no_factory_found()
            {
                // Arrange
                const string documentTagType = "unknownTag";
                const string defaultTagType = "defaultTag";

                var factories = new Dictionary<string, IDocumentTagDocumentFactory>();

                var settings = new EventStreamDefaultTypeSettings
                {
                    DocumentTagType = defaultTagType
                };

                var sut = new DocumentTagDocumentFactory(factories, settings);

                // Act & Assert
                var exception = Assert.Throws<UnableToFindDocumentTagFactoryException>(
                    () => sut.CreateDocumentTagStore(documentTagType));

                Assert.Contains(documentTagType, exception.Message);
            }
        }

        public class CreateDocumentTagStoreWithoutParameters
        {
            [Fact]
            public void Should_throw_not_implemented_exception()
            {
                // Arrange
                var factories = new Dictionary<string, IDocumentTagDocumentFactory>();
                var settings = Substitute.For<EventStreamDefaultTypeSettings>();
                var sut = new DocumentTagDocumentFactory(factories, settings);

                // Act & Assert
                Assert.Throws<NotImplementedException>(() => DocumentTagDocumentFactory.CreateDocumentTagStore());
            }
        }
    }
}
