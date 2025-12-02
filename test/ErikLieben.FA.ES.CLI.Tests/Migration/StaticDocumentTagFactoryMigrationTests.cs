using ErikLieben.FA.ES.CLI.Migration;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Migration;

public class StaticDocumentTagFactoryMigrationTests
{
    private readonly StaticDocumentTagFactoryMigration _migration = new();

    [Fact]
    public void Description_ShouldReturnExpectedValue()
    {
        Assert.Equal(
            "Replace static DocumentTagDocumentFactory.CreateDocumentTagStore() with instance method",
            _migration.Description);
    }

    [Fact]
    public void Apply_ShouldNotModifyFileWithoutStaticCall()
    {
        // Arrange
        var content = @"
namespace MyApp;

public class OrderService
{
    public void Process() { }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void Apply_ShouldReplaceStaticCallWithInstanceCall()
    {
        // Arrange
        var content = @"
public class TagService
{
    public void CreateStore()
    {
        var store = DocumentTagDocumentFactory.CreateDocumentTagStore();
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("_documentTagFactory.CreateDocumentTagStore()", result);
        Assert.DoesNotContain("DocumentTagDocumentFactory.CreateDocumentTagStore()", result);
    }

    [Fact]
    public void Apply_ShouldAddTodoCommentWhenFactoryNotInjected()
    {
        // Arrange
        var content = @"
namespace MyApp;

public class TagService
{
    public void CreateStore()
    {
        var store = DocumentTagDocumentFactory.CreateDocumentTagStore();
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("TODO: Inject IDocumentTagDocumentFactory", result);
    }

    [Fact]
    public void Apply_ShouldNotAddTodoWhenFactoryAlreadyInjected()
    {
        // Arrange
        var content = @"
namespace MyApp;

public class TagService
{
    private readonly IDocumentTagDocumentFactory _documentTagFactory;

    public TagService(IDocumentTagDocumentFactory documentTagFactory)
    {
        _documentTagFactory = documentTagFactory;
    }

    public void CreateStore()
    {
        var store = DocumentTagDocumentFactory.CreateDocumentTagStore();
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("_documentTagFactory.CreateDocumentTagStore()", result);
        Assert.DoesNotContain("TODO:", result);
    }

    [Fact]
    public void Apply_ShouldHandleWhitespaceInCall()
    {
        // Arrange
        var content = @"
var store = DocumentTagDocumentFactory.CreateDocumentTagStore(   );";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("_documentTagFactory.CreateDocumentTagStore()", result);
    }

    [Fact]
    public void Apply_ShouldHandleMultipleCalls()
    {
        // Arrange
        var content = @"
public class Service
{
    public void Method1()
    {
        var store1 = DocumentTagDocumentFactory.CreateDocumentTagStore();
    }

    public void Method2()
    {
        var store2 = DocumentTagDocumentFactory.CreateDocumentTagStore();
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.DoesNotContain("DocumentTagDocumentFactory.CreateDocumentTagStore()", result);
        Assert.Equal(2, CountOccurrences(result, "_documentTagFactory.CreateDocumentTagStore()"));
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
