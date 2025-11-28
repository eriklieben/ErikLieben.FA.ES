using System;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Projections;

public class ObjectIdWhenValueFactoryTests
{
    public class CreateWithDocumentMethod
    {
        [Fact]
        public void Should_return_object_id_from_document()
        {
            // Arrange
            var factory = new ObjectIdWhenValueFactory();
            var document = Substitute.For<IObjectDocument>();
            document.ObjectId.Returns("test-object-id");
            document.ObjectName.Returns("TestObject");
            var @event = Substitute.For<IEvent>();

            // Act
            var result = factory.Create(document, @event);

            // Assert
            Assert.Equal("test-object-id", result);
        }

        [Fact]
        public void Should_throw_when_document_is_null()
        {
            // Arrange
            var factory = new ObjectIdWhenValueFactory();
            var @event = Substitute.For<IEvent>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.Create((IObjectDocument)null!, @event));
        }

        [Fact]
        public void Should_throw_when_object_id_is_null()
        {
            // Arrange
            var factory = new ObjectIdWhenValueFactory();
            var document = Substitute.For<IObjectDocument>();
            document.ObjectId.Returns((string)null!);
            document.ObjectName.Returns("TestObject");
            var @event = Substitute.For<IEvent>();
            @event.EventType.Returns("TestEvent");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(document, @event));
            Assert.Contains("ObjectId cannot be null or empty", ex.Message);
            Assert.Contains("TestEvent", ex.Message);
        }

        [Fact]
        public void Should_throw_when_object_id_is_empty()
        {
            // Arrange
            var factory = new ObjectIdWhenValueFactory();
            var document = Substitute.For<IObjectDocument>();
            document.ObjectId.Returns(string.Empty);
            document.ObjectName.Returns("TestObject");
            var @event = Substitute.For<IEvent>();
            @event.EventType.Returns("AnotherEvent");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(document, @event));
            Assert.Contains("ObjectId cannot be null or empty", ex.Message);
        }
    }

    public class CreateWithVersionTokenMethod
    {
        [Fact]
        public void Should_return_object_id_from_version_token()
        {
            // Arrange
            var factory = new ObjectIdWhenValueFactory();
            var versionToken = new VersionToken("ObjectName", "test-object-id", "stream-123", 1);
            var @event = Substitute.For<IEvent>();

            // Act
            var result = factory.Create(versionToken, @event);

            // Assert
            Assert.Equal("test-object-id", result);
        }

        [Fact]
        public void Should_throw_when_version_token_is_null()
        {
            // Arrange
            var factory = new ObjectIdWhenValueFactory();
            var @event = Substitute.For<IEvent>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.Create((VersionToken)null!, @event));
        }

        [Fact]
        public void Should_throw_when_version_token_object_id_is_empty()
        {
            // Arrange
            var factory = new ObjectIdWhenValueFactory();
            // VersionToken constructor validates objectId, so empty string throws at construction time
            // This test verifies the VersionToken validation happens
            var @event = Substitute.For<IEvent>();

            // Act & Assert - VersionToken itself throws when objectId is empty
            Assert.Throws<ArgumentException>(() => new VersionToken("ObjectName", "", "stream-123", 1));
        }
    }
}
