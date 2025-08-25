using System;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class EventStreamDataAndExceptionsTests
{
    [Fact]
    public void EventStreamData_ctor_sets_properties_and_defaults()
    {
        var dto = new EventStreamData(
            objectId: "42",
            objectType: "Account",
            connection: "conn",
            documentType: "doc",
            defaultStreamType: "es",
            defaultStreamConnection: "es-conn",
            createEmtpyObjectWhenNonExisting: true);

        Assert.Equal("42", dto.ObjectId);
        Assert.Equal("Account", dto.ObjectType);
        Assert.Equal("conn", dto.Connection);
        Assert.Equal("doc", dto.DocumentType);
        Assert.Equal("es", dto.DefaultStreamType);
        Assert.Equal("es-conn", dto.DefaultStreamConnection);
        Assert.True(dto.CreateEmptyObjectWhenNonExistent);

        dto.CreateEmptyObjectWhenNonExistent = false;
        Assert.False(dto.CreateEmptyObjectWhenNonExistent);
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
