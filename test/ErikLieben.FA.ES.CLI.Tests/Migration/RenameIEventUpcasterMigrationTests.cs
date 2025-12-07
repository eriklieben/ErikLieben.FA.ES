using ErikLieben.FA.ES.CLI.Migration;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Migration;

public class RenameIEventUpcasterMigrationTests
{
    private readonly RenameIEventUpcasterMigration _migration = new();

    [Fact]
    public void Description_ShouldReturnExpectedValue()
    {
        Assert.Equal("Rename IEventUpcaster to IUpcastEvent", _migration.Description);
    }

    [Fact]
    public void Apply_ShouldNotModifyFileWithoutIEventUpcaster()
    {
        // Arrange
        var content = @"
using System;

namespace MyApp;

public class MyClass
{
    public void DoSomething() { }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void Apply_ShouldRenameInterfaceImplementation()
    {
        // Arrange
        var content = @"
using ErikLieben.FA.ES.Upcasting;

namespace MyApp;

public class MyEventUpcaster : IEventUpcaster
{
    public bool CanUpcast(IEvent @event) => true;
    public IEnumerable<IEvent> UpCast(IEvent @event) => new[] { @event };
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("IUpcastEvent", result);
        Assert.DoesNotContain("IEventUpcaster", result);
    }

    [Fact]
    public void Apply_ShouldRenameMultipleReferences()
    {
        // Arrange
        var content = @"
public class Processor
{
    private readonly IEventUpcaster _upcaster;

    public Processor(IEventUpcaster upcaster)
    {
        _upcaster = upcaster;
    }

    public void Register(IEventUpcaster upcaster)
    {
        // do something
    }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.DoesNotContain("IEventUpcaster", result);
        Assert.Equal(3, CountOccurrences(result, "IUpcastEvent"));
    }

    [Fact]
    public void Apply_ShouldHandleGenericConstraints()
    {
        // Arrange
        var content = @"
public class Factory<T> where T : IEventUpcaster
{
    public T Create() => default!;
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("where T : IUpcastEvent", result);
        Assert.DoesNotContain("IEventUpcaster", result);
    }

    [Fact]
    public void Apply_ShouldHandleVariableDeclarations()
    {
        // Arrange
        var content = @"
public void Process()
{
    IEventUpcaster upcaster = GetUpcaster();
    var list = new List<IEventUpcaster>();
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("IUpcastEvent upcaster", result);
        Assert.Contains("List<IUpcastEvent>", result);
        Assert.DoesNotContain("IEventUpcaster", result);
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
