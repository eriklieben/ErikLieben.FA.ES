﻿using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class AggregateJsonTypeInfoNotSetExceptionTests
{
    [Fact]
    public void Should_set_correct_error_message()
    {
        // Arrange & Act
        var sut = new AggregateJsonTypeInfoNotSetException();

        // Assert
        Assert.Equal("Aggregate JsonInfo type should be set to deserialize the aggregate type", sut.Message);
    }

    [Fact]
    public void Should_inherit_from_exception()
    {
        // Arrange & Act
        var sut = new AggregateJsonTypeInfoNotSetException();

        // Assert
        Assert.IsAssignableFrom<Exception>(sut);
    }
}