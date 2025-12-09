using System;
using System.Text.Json;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class EventStreamDataAndExceptionsTests
{
    public class ParameterizedConstructor : EventStreamDataAndExceptionsTests
    {
        [Fact]
        public void Should_set_all_properties()
        {
            // Arrange & Act
            var sut = new EventStreamData(
                objectId: "42",
                objectType: "Account",
                connection: "conn",
                documentType: "doc",
                defaultStreamType: "es",
                defaultStreamConnection: "es-conn",
                createEmtpyObjectWhenNonExisting: true);

            // Assert
            Assert.Equal("42", sut.ObjectId);
            Assert.Equal("Account", sut.ObjectType);
            Assert.Equal("conn", sut.Connection);
            Assert.Equal("doc", sut.DocumentType);
            Assert.Equal("es", sut.DefaultStreamType);
            Assert.Equal("es-conn", sut.DefaultStreamConnection);
            Assert.True(sut.CreateEmptyObjectWhenNonExistent);
        }

        [Fact]
        public void Should_allow_modifying_CreateEmptyObjectWhenNonExistent()
        {
            // Arrange
            var sut = new EventStreamData(
                objectId: "42",
                objectType: "Account",
                connection: "conn",
                documentType: "doc",
                defaultStreamType: "es",
                defaultStreamConnection: "es-conn",
                createEmtpyObjectWhenNonExisting: true);

            // Act
            sut.CreateEmptyObjectWhenNonExistent = false;

            // Assert
            Assert.False(sut.CreateEmptyObjectWhenNonExistent);
        }
    }

    public class JsonSerialization : EventStreamDataAndExceptionsTests
    {
        [Fact]
        public void Should_serialize_and_deserialize_correctly()
        {
            // Arrange
            var original = new EventStreamData(
                objectId: "abc-123",
                objectType: "Order",
                connection: "my-connection",
                documentType: "orders",
                defaultStreamType: "table",
                defaultStreamConnection: "table-conn",
                createEmtpyObjectWhenNonExisting: true);

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<EventStreamData>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.ObjectId, deserialized!.ObjectId);
            Assert.Equal(original.ObjectType, deserialized.ObjectType);
            Assert.Equal(original.Connection, deserialized.Connection);
            Assert.Equal(original.DocumentType, deserialized.DocumentType);
            Assert.Equal(original.DefaultStreamType, deserialized.DefaultStreamType);
            Assert.Equal(original.DefaultStreamConnection, deserialized.DefaultStreamConnection);
            Assert.Equal(original.CreateEmptyObjectWhenNonExistent, deserialized.CreateEmptyObjectWhenNonExistent);
        }

        [Fact]
        public void Should_deserialize_with_default_values()
        {
            // Arrange
            var json = "{}";

            // Act
            var deserialized = JsonSerializer.Deserialize<EventStreamData>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Null(deserialized!.ObjectId);
            Assert.Null(deserialized.ObjectType);
            Assert.Null(deserialized.Connection);
            Assert.Null(deserialized.DocumentType);
            Assert.Null(deserialized.DefaultStreamType);
            Assert.Null(deserialized.DefaultStreamConnection);
            Assert.False(deserialized.CreateEmptyObjectWhenNonExistent);
        }

        [Fact]
        public void Should_deserialize_with_partial_values()
        {
            // Arrange
            var json = """{"ObjectId":"partial-id","ObjectType":"PartialOrder"}""";

            // Act
            var deserialized = JsonSerializer.Deserialize<EventStreamData>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("partial-id", deserialized!.ObjectId);
            Assert.Equal("PartialOrder", deserialized.ObjectType);
            Assert.Null(deserialized.Connection);
            Assert.False(deserialized.CreateEmptyObjectWhenNonExistent);
        }
    }

    public class Properties : EventStreamDataAndExceptionsTests
    {
        [Fact]
        public void Should_allow_setting_all_properties()
        {
            // Arrange
            var sut = new EventStreamData(
                objectId: "initial",
                objectType: "Initial",
                connection: "",
                documentType: "",
                defaultStreamType: "",
                defaultStreamConnection: "",
                createEmtpyObjectWhenNonExisting: false);

            // Act
            sut.ObjectId = "updated-id";
            sut.ObjectType = "UpdatedType";
            sut.Connection = "updated-conn";
            sut.DocumentType = "updated-doc";
            sut.DefaultStreamType = "updated-stream";
            sut.DefaultStreamConnection = "updated-stream-conn";

            // Assert
            Assert.Equal("updated-id", sut.ObjectId);
            Assert.Equal("UpdatedType", sut.ObjectType);
            Assert.Equal("updated-conn", sut.Connection);
            Assert.Equal("updated-doc", sut.DocumentType);
            Assert.Equal("updated-stream", sut.DefaultStreamType);
            Assert.Equal("updated-stream-conn", sut.DefaultStreamConnection);
        }
    }

    [Fact]
    public void InvalidBindingSourceException_should_format_message()
    {
        var ex = new InvalidBindingSourceException("Other", "ErikLieben.FA.ES.Azure.Functions.Worker.Extensions");
        Assert.Contains("Unexpected binding source 'Other'", ex.Message);
        Assert.Contains("ErikLieben.FA.ES.Azure.Functions.Worker.Extensions", ex.Message);

        var inner = new Exception("boom");
        var ex2 = new InvalidBindingSourceException("A", "B", inner);
        Assert.Same(inner, ex2.InnerException);
        Assert.Contains("'A'", ex2.Message);
        Assert.Contains("'B'", ex2.Message);
    }

    [Fact]
    public void InvalidContentTypeException_should_format_message()
    {
        var ex = new InvalidContentTypeException("text/plain", "application/json");
        Assert.Contains("text/plain", ex.Message);
        Assert.Contains("application/json", ex.Message);

        var inner = new Exception("fail");
        var ex2 = new InvalidContentTypeException("X", "Y", inner);
        Assert.Same(inner, ex2.InnerException);
        Assert.Contains("'X'", ex2.Message);
        Assert.Contains("'Y'", ex2.Message);
    }
}
