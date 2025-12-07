using ErikLieben.FA.ES.CLI.Migration;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Migration;

public class UpdateUpcasterNamingConventionMigrationTests
{
    private readonly UpdateUpcasterNamingConventionMigration _migration = new();

    [Fact]
    public void Description_ShouldReturnExpectedValue()
    {
        Assert.Equal("Update *Upcaster class names to *Upcast naming convention", _migration.Description);
    }

    [Fact]
    public void Apply_ShouldNotModifyFileWithoutUpcaster()
    {
        // Arrange
        var content = @"
namespace MyApp;

public class OrderProcessor
{
    public void Process() { }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void Apply_ShouldRenameEventUpcasterClass()
    {
        // Arrange
        var content = @"
namespace MyApp;

public class OrderCreatedEventUpcaster : IUpcastEvent
{
    public bool CanUpcast(IEvent e) => true;
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("class OrderCreatedEventUpcast", result);
        Assert.DoesNotContain("EventUpcaster", result);
    }

    [Fact]
    public void Apply_ShouldRenameClassInstantiation()
    {
        // Arrange
        var content = @"
public void Register()
{
    var upcaster = new OrderCreatedEventUpcaster();
    stream.RegisterUpcast(new CustomerUpdatedEventUpcaster());
}";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("new OrderCreatedEventUpcast()", result);
        Assert.Contains("new CustomerUpdatedEventUpcast()", result);
        Assert.DoesNotContain("EventUpcaster", result);
    }

    [Fact]
    public void Apply_ShouldNotRenameNonEventUpcasterClasses()
    {
        // Arrange
        var content = @"
public class DataUpcaster
{
    public void Upcast() { }
}";

        // Act
        var result = _migration.Apply(content);

        // Assert - Should keep DataUpcaster as-is (doesn't match *EventUpcaster pattern)
        Assert.Contains("class DataUpcaster", result);
    }

    [Fact]
    public void Apply_ShouldHandleMultipleClasses()
    {
        // Arrange
        var content = @"
public class OrderCreatedEventUpcaster { }
public class OrderUpdatedEventUpcaster { }
public class OrderDeletedEventUpcaster { }";

        // Act
        var result = _migration.Apply(content);

        // Assert
        Assert.Contains("class OrderCreatedEventUpcast", result);
        Assert.Contains("class OrderUpdatedEventUpcast", result);
        Assert.Contains("class OrderDeletedEventUpcast", result);
        Assert.DoesNotContain("EventUpcaster", result);
    }

    [Fact]
    public void Apply_ShouldPreserveInheritanceColon()
    {
        // Arrange - class with inheritance should keep the colon
        var content = @"
public class OrderEventUpcaster : BaseUpcaster
{
}";

        // Act
        var result = _migration.Apply(content);

        // Assert - Should rename class but preserve inheritance
        Assert.Contains("class OrderEventUpcast : BaseUpcaster", result);
    }
}
