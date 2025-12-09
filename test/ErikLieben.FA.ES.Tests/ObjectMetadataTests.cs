#pragma warning disable CS0618 // Type or member is obsolete - testing deprecated API intentionally

using System;
using ErikLieben.FA.ES.Documents;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests;

public class ObjectMetadataTests
{ 
    private readonly StreamInformation streamInformation = new()
    {
        CurrentStreamVersion = 1,
        DocumentTagType = "DocType",
        DocumentTagConnectionName = "Store",
        StreamIdentifier = Guid.NewGuid().ToString(),
        StreamType = "Blob",
        StreamConnectionName = "Store",
        StreamTagConnectionName = "TagStore",
        SnapShotConnectionName = "SnapshotStore"
    };
    
    [Fact]
    public void Should_throw_ArgumentNullException_when_event_is_null()
    {
        // Arrange
        var document = Substitute.For<IObjectDocument>();
        document.Active.Returns(streamInformation);
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ObjectMetadata<string>.From(document, null!, "test-id"));
    }
    
    [Fact]
    public void Should_create_ObjectMetadata_with_correct_values()
    {
        // Arrange
        var document = Substitute.For<IObjectDocument>();
        document.Active.Returns(streamInformation);
        var mockEvent = Substitute.For<IEvent>();
        mockEvent.EventVersion.Returns(1);
        
        // Act
        var sut = ObjectMetadata<string>.From(document, mockEvent, "test-id");
        
        // Assert
        Assert.NotNull(sut);
        Assert.Equal("test-id", sut.Id);
        Assert.Equal(document.Active.StreamIdentifier, sut.StreamId);
        Assert.Equal(1, sut.VersionInStream);
        Assert.Equal($"{document.Active.StreamIdentifier}:00000000000000000001", sut.Version);
    } 
    
    [Fact]
    public void Should_create_VersionToken_with_correct_values()
    {
        // Arrange
        var document = Substitute.For<IObjectDocument>();
        document.Active.Returns(streamInformation);
        var mockEvent = Substitute.For<IEvent>();
        mockEvent.EventVersion.Returns(1);
        var sut = ObjectMetadata<string>.From(document, mockEvent, "test-id");
        var objectName = "test-name";
        
        // Act
        var result = sut.ToVersionToken(objectName);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-id", result.ObjectId);
        Assert.Equal(objectName, result.ObjectName);
        Assert.Equal(streamInformation.StreamIdentifier, result.StreamIdentifier);
        Assert.Equal(1, result.Version);
    } 
    
    
    [Fact]
    public void Should_throw_InvalidOperationException_when_StreamId_is_null()
    {
        // Arrange
        var document = Substitute.For<IObjectDocument>();
        document.Active.Returns(new StreamInformation
        {
            CurrentStreamVersion = 1,
            DocumentTagType = "DocType",
            DocumentTagConnectionName = "Store",
            StreamIdentifier = null!,
            StreamType = "Blob",
            StreamConnectionName = "Store",
            StreamTagConnectionName = "TagStore",
            SnapShotConnectionName = "SnapshotStore"
        });
        var mockEvent = Substitute.For<IEvent>();
        mockEvent.EventVersion.Returns(1);
        var sut = ObjectMetadata<string>.From(document, mockEvent, "test-id");
        var objectName = "test-name";
        
        // Act
        var exception = 
            Assert.Throws<InvalidOperationException>(() => sut.ToVersionToken(objectName));
        
        // Assert
        Assert.NotNull(exception);
        Assert.Equal("StreamId is null or whitespace", exception.Message);
    } 
    
    [Fact]
    public void Should_throw_InvalidOperationException_when_StreamId_is_empty()
    {
        // Arrange
        var document = Substitute.For<IObjectDocument>();
        document.Active.Returns(new StreamInformation
        {
            CurrentStreamVersion = 1,
            DocumentTagType = "DocType",
            DocumentTagConnectionName = "Store",
            StreamIdentifier = string.Empty,
            StreamType = "Blob",
            StreamConnectionName = "Store",
            StreamTagConnectionName = "TagStore",
            SnapShotConnectionName = "SnapshotStore"
        });
        var mockEvent = Substitute.For<IEvent>();
        mockEvent.EventVersion.Returns(1);
        var sut = ObjectMetadata<string>.From(document, mockEvent, "test-id");
        var objectName = "test-name";
        
        // Act
        var exception = 
            Assert.Throws<InvalidOperationException>(() => sut.ToVersionToken(objectName));
        
        // Assert
        Assert.NotNull(exception);
        Assert.Equal("StreamId is null or whitespace", exception.Message);
    } 
    
    [Fact]
    public void Should_()
    {
        // Arrange
        var document = Substitute.For<IObjectDocument>();
        document.Active.Returns(new StreamInformation
        {
            CurrentStreamVersion = 1,
            DocumentTagType = "DocType",
            DocumentTagConnectionName = "Store",
            StreamIdentifier = Guid.NewGuid().ToString(),
            StreamType = "Blob",
            StreamConnectionName = "Store",
            StreamTagConnectionName = "TagStore",
            SnapShotConnectionName = "SnapshotStore"
        });
        var mockEvent = Substitute.For<IEvent>();
        mockEvent.EventVersion.Returns(1);
        var sut = ObjectMetadata<string>.From(document, mockEvent, null!);
        var objectName = "test-name";
        
        // Act
        var exception = 
            Assert.Throws<InvalidOperationException>(() => sut.ToVersionToken(objectName));
        
        // Assert
        Assert.NotNull(exception);
        Assert.Equal("Id is null", exception.Message);
    } 
}