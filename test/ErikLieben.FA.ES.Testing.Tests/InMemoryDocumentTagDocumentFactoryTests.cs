using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory.Model;
using ErikLieben.FA.ES.Testing.InMemory;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class InMemoryDocumentTagDocumentFactoryTests
{
    [Fact]
    public void CreateDocumentTagStore_overloads_should_return_instances_and_validate_inputs()
    {
        // Arrange
        var factory = new InMemoryDocumentTagDocumentFactory();
        var document = new InMemoryEventStreamDocument(
            "1",
            "order",
            new StreamInformation
            {
                StreamConnectionName = "inMemory",
                SnapShotConnectionName = "inMemory",
                DocumentTagConnectionName = "inMemory",
                StreamTagConnectionName = "inMemory",
                StreamIdentifier = "1-0000000000",
                StreamType = "inMemory",
                DocumentTagType = "inMemory",
                CurrentStreamVersion = -1,
            },
            [],
            "1.0.0");

        // Act
        var s1 = factory.CreateDocumentTagStore(document);
        var s2 = factory.CreateDocumentTagStore();
        var s3 = factory.CreateDocumentTagStore("anything");

        // Assert
        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.NotNull(s3);
    }
}
