using System;
using ErikLieben.FA.ES.Documents;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests;

public class VersionTokenOfTTests
{
    // Concrete implementation of the abstract class for testing
    private record TestVersionToken : VersionToken<Guid>
    {
        public TestVersionToken() : base()
        {
        }

        public TestVersionToken(string versionTokenString) : base(versionTokenString)
        {
        }

        public TestVersionToken(IEvent @event, IObjectDocument document) : base(@event, document)
        {
        }

        protected override Guid ToObjectOfT(string objectId)
        {
            return Guid.Parse(objectId);
        }

        protected override string FromObjectOfT(Guid objectId)
        {
            return objectId.ToString();
        }
    }

    [Fact]
    public void Should_Convert_String_ObjectId_To_Type_T()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var sut = new TestVersionToken();

        // Act
        sut.ObjectId = expectedGuid;
        var result = sut.ObjectId;

        // Assert
        Assert.Equal(expectedGuid, result);
    }

    [Fact]
    public void Should_Convert_Type_T_To_String_ObjectId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var sut = new TestVersionToken();

        // Act
        sut.ObjectId = guid;
        var baseToken = (VersionToken)sut;

        // Assert
        Assert.Equal(guid.ToString(), baseToken.ObjectId);
    }

    [Fact]
    public void Should_initialize_objectId_from_versionTokenString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var tokenString = $"def__{guid}__000__001";

        // Act
        var newToken = new TestVersionToken(tokenString);

        // Assert
        Assert.Equal(guid, newToken.ObjectId);
    }

    [Fact]
    public void Should_initialize_objectName_from_versionTokenString()
    {
        // Arrange
        var tokenString = $"def__{Guid.NewGuid()}__000__001";

        // Act
        var newToken = new TestVersionToken(tokenString);

        // Assert
        Assert.Equal("def", newToken.ObjectName);
    }

    [Fact]
    public void Should_initialize_streamIdentifier_from_versionTokenString()
    {
        // Arrange
        var tokenString = $"def__{Guid.NewGuid()}__000__001";

        // Act
        var newToken = new TestVersionToken(tokenString);

        // Assert
        Assert.Equal("000", newToken.StreamIdentifier);
    }

    [Fact]
    public void Should_initialize_streamVersion_from_versionTokenString()
    {
        // Arrange
        var tokenString = $"def__{Guid.NewGuid()}__000__001";

        // Act
        var newToken = new TestVersionToken(tokenString);

        // Assert
        Assert.Equal(1, newToken.Version);
    }

    [Fact]
    public void Should_Initialize_From_Event_And_Document()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var mockEvent = Substitute.For<IEvent>();
        var mockDocument = Substitute.For<IObjectDocument>();
        var activeStream = Substitute.For<StreamInformation>();
        activeStream.StreamIdentifier = "abc";
        mockDocument.ObjectId.Returns(guid.ToString());
        mockDocument.ObjectName.Returns("test");
        mockDocument.Active.Returns(activeStream);
        mockEvent.EventVersion.Returns(123);

        // Act
        var sut = new TestVersionToken(mockEvent, mockDocument);

        // Assert
        Assert.Equal(guid, sut.ObjectId);
    }

    [Fact]
    public void Should_Override_Base_ObjectId_When_Setting_Generic_ObjectId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var sut = new TestVersionToken();

        // Act
        sut.ObjectId = guid;
        var baseToken = (VersionToken)sut;

        // Assert
        Assert.Equal(guid.ToString(), baseToken.ObjectId);
    }

    [Fact]
    public void Should_Preserve_Original_Type_After_Casting_To_Base_And_Back()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var sut = new TestVersionToken();
        sut.ObjectId = guid;

        // Act
        var baseToken = (VersionToken)sut;
        var convertedBack = (TestVersionToken)baseToken;

        // Assert
        Assert.Equal(guid, convertedBack.ObjectId);
    }

    [Fact]
    public void Should_Work_With_Generic_And_Non_Generic_Properties_Simultaneously()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var sut = new TestVersionToken();

        // Act
        sut.ObjectId = guid;
        var genericId = sut.ObjectId;
        var baseId = ((VersionToken)sut).ObjectId;

        // Assert
        Assert.Equal(guid, genericId);
        Assert.Equal(guid.ToString(), baseId);
    }

    [Fact]
    public void Should_Properly_Use_FromObjectOfT_When_Setting_ObjectId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var sut = new TestVersionToken();

        // Act
        sut.ObjectId = guid;

        // Assert
        Assert.Equal(guid.ToString(), ((VersionToken)sut).ObjectId);
    }

    public class Ctor
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_objectIdentifierPart_is_null()
        {
            // Arrange
            string? objectIdentifierPart = null;
            var versionIdentifierPart = "stream1__00000000000000000001";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VersionToken(objectIdentifierPart!, versionIdentifierPart));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_versionIdentifierPart_is_null()
        {
            // Arrange
            string objectIdentifierPart = "TestObject__123";
            string? versionIdentifierPart = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VersionToken(objectIdentifierPart, versionIdentifierPart!));
        }

        [Fact]
        public void Should_set_initial_version_to_negative_one()
        {
            // Arrange
            string objectIdentifierPart = "TestObject__123";
            string versionIdentifierPart = "stream1__00000000000000000001";

            // Act
            var sut = new VersionToken(objectIdentifierPart, versionIdentifierPart);

            // Assert
            Assert.Equal(1, sut.Version);
        }

        [Fact]
        public void Should_construct_value_by_combining_parts_with_double_underscore()
        {
            // Arrange
            var objectIdentifierPart = "TestObject__123";
            string versionIdentifierPart = "stream1__00000000000000000001";

            // Act
            var sut = new VersionToken(objectIdentifierPart, versionIdentifierPart);

            // Assert
            Assert.Equal($"{objectIdentifierPart}__{versionIdentifierPart}", sut.Value);
        }

        [Fact]
        public void Should_parse_full_string_from_constructed_value()
        {
            // Arrange
            string objectName = "TestObject";
            string objectId = "123";
            string streamIdentifier = "stream1";
            string versionString = "00000000000000000001";
            string objectIdentifierPart = $"{objectName}__{objectId}";
            string versionIdentifierPart = $"{streamIdentifier}__{versionString}";

            // Act
            var sut = new VersionToken(objectIdentifierPart, versionIdentifierPart);

            // Assert
            Assert.Equal(objectName, sut.ObjectName);
            Assert.Equal(objectId, sut.ObjectId);
            Assert.Equal(streamIdentifier, sut.StreamIdentifier);
            Assert.Equal(1, sut.Version);
            Assert.Equal(versionString, sut.VersionString);
        }

        [Fact]
        public void Should_throw_ArgumentException_when_parts_format_is_invalid()
        {
            // Arrange
            string objectIdentifierPart = "TestObject_123"; // Missing double underscore
            string versionIdentifierPart = "stream1__00000000000000000001";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new VersionToken(objectIdentifierPart, versionIdentifierPart));
        }

        [Fact]
        public void Should_create_valid_object_and_version_identifiers()
        {
            // Arrange
            string objectName = "TestObject";
            string objectId = "123";
            string streamIdentifier = "stream1";
            string versionString = "00000000000000000001";
            string objectIdentifierPart = $"{objectName}__{objectId}";
            string versionIdentifierPart = $"{streamIdentifier}__{versionString}";

            // Act
            var sut = new VersionToken(objectIdentifierPart, versionIdentifierPart);

            // Assert
            Assert.Equal(objectName, sut.ObjectIdentifier.ObjectName);
            Assert.Equal(objectId, sut.ObjectIdentifier.ObjectId);
            Assert.Equal(streamIdentifier, sut.VersionIdentifier.StreamIdentifier);
            Assert.Equal("00000000000000000001", sut.VersionIdentifier.VersionString);
        }
    }

    public class FromMethod
    {
        [Fact]
        public void Should_format_document_and_event_into_version_string()
        {
            // Arrange
            var @event = NSubstitute.Substitute.For<IEvent>();
            @event.EventVersion.Returns(42);

            var streamInformation = Substitute.For<StreamInformation>();
            streamInformation.StreamIdentifier = "test-stream";

            var document = NSubstitute.Substitute.For<IObjectDocument>();
            document.ObjectName.Returns("TestObject");
            document.ObjectId.Returns("test-id");
            document.Active.Returns(streamInformation);

            // Act
            var result = VersionToken.From(@event, document);

            // Assert
            Assert.Equal("TestObject__test-id__test-stream__00000000000000000042", result);
        }

        [Fact]
        public void Should_handle_different_event_versions()
        {
            // Arrange
            var @event = Substitute.For<IEvent>();
            @event.EventVersion.Returns(999);

            var streamInformation = Substitute.For<StreamInformation>();
            streamInformation.StreamIdentifier = "stream1";

            var document = NSubstitute.Substitute.For<IObjectDocument>();
            document.ObjectName.Returns("Object1");
            document.ObjectId.Returns("id1");
            document.Active.Returns(streamInformation);

            // Act
            var result = VersionToken.From(@event, document);

            // Assert
            Assert.Equal("Object1__id1__stream1__00000000000000000999", result);
        }

        [Fact]
        public void Should_handle_zero_event_version()
        {
            // Arrange
            var @event = Substitute.For<IEvent>();
            @event.EventVersion.Returns(0);

            var streamInformation = Substitute.For<StreamInformation>();
            streamInformation.StreamIdentifier = "stream-zero";

            var document = Substitute.For<IObjectDocument>();
            document.ObjectName.Returns("ZeroObject");
            document.ObjectId.Returns("zero-id");
            document.Active.Returns(streamInformation);

            // Act
            var result = VersionToken.From(@event, document);

            // Assert
            Assert.Equal("ZeroObject__zero-id__stream-zero__00000000000000000000", result);
        }
    }
}
