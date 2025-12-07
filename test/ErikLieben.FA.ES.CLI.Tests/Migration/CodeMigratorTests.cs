using ErikLieben.FA.ES.CLI.Migration;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Migration;

public class CodeMigratorTests
{
    [Fact]
    public async Task MigrateAsync_ShouldSucceedWithNoMigrations()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            // v2 to v2 - no migrations needed
            var migrator = new CodeMigrator(tempDir, "2.0.0", "2.0.0", dryRun: true);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.FilesModified);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ShouldApplyMigrationsForV1ToV2()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            // Create a file with IEventUpcaster
            var testFile = Path.Combine(tempDir, "TestUpcaster.cs");
            await File.WriteAllTextAsync(testFile, @"
public class OrderEventUpcaster : IEventUpcaster
{
    public bool CanUpcast(IEvent e) => true;
}");

            var migrator = new CodeMigrator(tempDir, "1.3.6", "2.0.0", dryRun: false);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.FilesModified > 0);

            var content = await File.ReadAllTextAsync(testFile);
            Assert.Contains("IUpcastEvent", content);
            Assert.Contains("OrderEventUpcast", content);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_DryRun_ShouldNotModifyFiles()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var originalContent = @"public class OrderEventUpcaster : IEventUpcaster { }";
            var testFile = Path.Combine(tempDir, "TestUpcaster.cs");
            await File.WriteAllTextAsync(testFile, originalContent);

            var migrator = new CodeMigrator(tempDir, "1.3.6", "2.0.0", dryRun: true);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.FilesModified > 0); // Counts what would be modified

            var content = await File.ReadAllTextAsync(testFile);
            Assert.Equal(originalContent, content); // File should be unchanged
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ShouldIgnoreObjFolder()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var objDir = Path.Combine(tempDir, "obj");
            Directory.CreateDirectory(objDir);

            var testFile = Path.Combine(objDir, "Generated.cs");
            var originalContent = @"public class Test : IEventUpcaster { }";
            await File.WriteAllTextAsync(testFile, originalContent);

            var migrator = new CodeMigrator(tempDir, "1.3.6", "2.0.0", dryRun: false);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            var content = await File.ReadAllTextAsync(testFile);
            Assert.Equal(originalContent, content); // Should not be modified
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ShouldIgnoreBinFolder()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var binDir = Path.Combine(tempDir, "bin");
            Directory.CreateDirectory(binDir);

            var testFile = Path.Combine(binDir, "Compiled.cs");
            var originalContent = @"public class Test : IEventUpcaster { }";
            await File.WriteAllTextAsync(testFile, originalContent);

            var migrator = new CodeMigrator(tempDir, "1.3.6", "2.0.0", dryRun: false);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            var content = await File.ReadAllTextAsync(testFile);
            Assert.Equal(originalContent, content); // Should not be modified
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ShouldIgnoreGeneratedFiles()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var testFile = Path.Combine(tempDir, "Order.generated.cs");
            var originalContent = @"public class Test : IEventUpcaster { }";
            await File.WriteAllTextAsync(testFile, originalContent);

            var migrator = new CodeMigrator(tempDir, "1.3.6", "2.0.0", dryRun: false);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            var content = await File.ReadAllTextAsync(testFile);
            Assert.Equal(originalContent, content); // Should not be modified
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ShouldHandleEmptyDirectory()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var migrator = new CodeMigrator(tempDir, "1.3.6", "2.0.0", dryRun: false);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.FilesModified);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ShouldProcessSubdirectories()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var subDir = Path.Combine(tempDir, "src", "Domain");
            Directory.CreateDirectory(subDir);

            var testFile = Path.Combine(subDir, "OrderUpcaster.cs");
            await File.WriteAllTextAsync(testFile, @"public class OrderEventUpcaster : IEventUpcaster { }");

            var migrator = new CodeMigrator(tempDir, "1.3.6", "2.0.0", dryRun: false);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.FilesModified > 0);

            var content = await File.ReadAllTextAsync(testFile);
            Assert.Contains("IUpcastEvent", content);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ShouldApplyAllMigrationsInOrder()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        try
        {
            var testFile = Path.Combine(tempDir, "ComplexFile.cs");
            await File.WriteAllTextAsync(testFile, @"
namespace MyApp;

public class OrderCreatedEventUpcaster : IEventUpcaster
{
    private readonly IObjectDocument document;

    public void Process(IEvent e)
    {
        projection.Fold(e, document);
        var store = DocumentTagDocumentFactory.CreateDocumentTagStore();
    }
}");

            var migrator = new CodeMigrator(tempDir, "1.3.6", "2.0.0", dryRun: false);

            // Act
            var result = await migrator.MigrateAsync(CancellationToken.None);

            // Assert
            Assert.True(result.Success);

            var content = await File.ReadAllTextAsync(testFile);

            // Migration 1: IEventUpcaster -> IUpcastEvent
            Assert.Contains("IUpcastEvent", content);
            Assert.DoesNotContain("IEventUpcaster", content);

            // Migration 2: *EventUpcaster -> *EventUpcast
            Assert.Contains("OrderCreatedEventUpcast", content);
            Assert.DoesNotContain("OrderCreatedEventUpcaster", content);

            // Migration 3: Static -> Instance
            Assert.Contains("_documentTagFactory.CreateDocumentTagStore()", content);
            Assert.DoesNotContain("DocumentTagDocumentFactory.CreateDocumentTagStore()", content);

            // Migration 4: Deprecated Fold TODO
            Assert.Contains("TODO: Migrate to Fold(event, versionToken)", content);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeMigratorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}

public class MigrationResultTests
{
    [Fact]
    public void MigrationResult_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var result = new MigrationResult();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.FilesModified);
    }

    [Fact]
    public void MigrationResult_ShouldAllowSettingValues()
    {
        // Arrange & Act
        var result = new MigrationResult
        {
            Success = true,
            FilesModified = 5
        };

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, result.FilesModified);
    }
}
