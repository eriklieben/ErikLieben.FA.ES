﻿using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryObjectDocumentFactoryTests
{
    [Fact]
    public async Task GetOrCreate_should_lowercase_object_name_and_create_document()
    {
        // Arrange
        var factory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());

        // Act
        var doc = await factory.GetOrCreateAsync("OrDeR", "ID1");

        // Assert
        Assert.Equal("order", doc.ObjectName);
        Assert.Equal("ID1", doc.ObjectId);
    }

    [Fact]
    public async Task GetFirstByObjectDocumentTag_should_return_null_when_no_tag_exists()
    {
        // Arrange
        var factory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());

        // Act
        var result = await factory.GetFirstByObjectDocumentTag("order", "missing");

        // Assert
        Assert.Null(result);
    }
}
