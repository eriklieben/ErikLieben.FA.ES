using ErikLieben.FA.ES.CLI.Migration;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Migration;

public class DeprecatedFoldOverloadMigrationTests
{
    private readonly DeprecatedFoldOverloadMigration _migration = new();

    [Fact]
    public void Description_ShouldReturnExpectedValue()
    {
        Assert.Equal(
            "Flag deprecated Fold(IObjectDocument) overloads for migration to VersionToken",
            _migration.Description);
    }

    [Fact]
    public void Apply_ShouldNotModifyFileWithoutIObjectDocument()
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
    public void Apply_ShouldNotModifyFileWithoutFoldCall()
    {
        // Arrange
        var content = @"
namespace MyApp;

public class Service
{
    private IObjectDocument _document;
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void Apply_ShouldAddTodoForFoldWithDocumentParameter()
    {
        // Arrange
        var content = @"
public class ProjectionHandler
{
    private IObjectDocument document;

    public async Task HandleAsync(IEvent @event)
    {
        await projection.Fold(@event, document);
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("TODO: Migrate to Fold(event, versionToken)", result);
    }

    [Fact]
    public void Apply_ShouldAddTodoForFoldWithDocVariableNamePattern()
    {
        // Arrange
        var content = @"
public class Handler
{
    private IObjectDocument _doc;

    public async Task Process(IEvent e)
    {
        await projection.Fold(e, _doc);
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("TODO: Migrate to Fold(event, versionToken)", result);
    }

    [Fact]
    public void Apply_ShouldNotAddDuplicateTodo()
    {
        // Arrange - already has a TODO comment
        var content = @"
public class Handler
{
    private IObjectDocument document;

    public async Task Process(IEvent e)
    {
        // TODO: already marked
        await projection.Fold(e, document);
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert - Should not add another TODO
        Assert.Equal(1, CountOccurrences(result, "TODO"));
    }

    [Fact]
    public void Apply_ShouldHandleGenericFoldCall()
    {
        // Arrange
        var content = @"
public class Handler
{
    private IObjectDocument document;

    public async Task Process(IEvent e, OrderData data)
    {
        await projection.Fold<OrderData>(e, document, data);
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("TODO: Migrate to Fold(event, versionToken)", result);
    }

    [Fact]
    public void Apply_ShouldNotFlagFoldWithVersionToken()
    {
        // Arrange - Using VersionToken already
        var content = @"
public class Handler
{
    private IObjectDocument document;
    private VersionToken versionToken;

    public async Task Process(IEvent e)
    {
        await projection.Fold(e, versionToken);
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert - Should not add TODO for versionToken parameter
        Assert.DoesNotContain("TODO: Migrate", result);
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
