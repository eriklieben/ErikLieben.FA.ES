using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.CodeGeneration;

public class GenerateVersionTokenOfTCodeTests
{
    private static (SolutionDefinition solution, string outDir) BuildSolution(ProjectDefinition project)
    {
        var solution = new SolutionDefinition
        {
            SolutionName = "Demo",
            Generator = new GeneratorInformation { Version = "1.0.0-test" },
            Projects = [project]
        };

        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(outDir);
        return (solution, outDir);
    }

    [Fact]
    public async Task Generate_writes_partial_record_and_extension_methods()
    {
        // Arrange
        var token = new VersionTokenDefinition
        {
            Name = "OrderVersion",
            Namespace = "Demo.App.Tokens",
            GenericType = "Guid",
            NamespaceOfType = "System",
            IsPartialClass = true,
            FileLocations = ["Demo\\Tokens\\OrderVersion.cs"]
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            VersionTokens = [token]
        };

        var (solution, outDir) = BuildSolution(project);
        // Ensure target directory exists because generator doesn't create directories
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Tokens"));

        var sut = new GenerateVersionTokenOfTCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Tokens", "OrderVersion.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Record declaration and constructors
        Assert.Contains("public partial record OrderVersion", code);
        Assert.Contains("public OrderVersion()", code);
        Assert.Contains("public OrderVersion(string versionTokenString) : base(versionTokenString)", code);
        Assert.Contains("public OrderVersion(Guid objectId, string versionIdentifierPart)", code);
        Assert.Contains("public OrderVersion(Guid objectId, string streamIdentifier, int version)", code);
        Assert.Contains("public OrderVersion(Guid objectId, VersionIdentifier versionIdentifier)", code);

        // Value formatting and ParseFullString usage
        Assert.Contains("ParseFullString(Value);", code);
        Assert.Contains("$\"{ObjectName}__{objectId}__{versionIdentifierPart}\"", code);
        Assert.Contains("var objectIdentifierPart = $\"{ObjectName}__{ObjectId}\";", code);

        // Extension class and method
        Assert.Contains("public static class ObjectMetaDataOrderVersionExtensions", code);
        Assert.Contains("ToVersionToken(this ObjectMetadata<Guid> token)", code);
        Assert.Contains("return new OrderVersion(token.Id, token.StreamId, token.VersionInStream);", code);
    }

    [Fact]
    public async Task Generate_skips_when_not_partial()
    {
        // Arrange
        var token = new VersionTokenDefinition
        {
            Name = "AccountVersion",
            Namespace = "Demo.App.Tokens",
            GenericType = "Guid",
            NamespaceOfType = "System",
            IsPartialClass = false,
            FileLocations = ["Demo\\Tokens\\AccountVersion.cs"]
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            VersionTokens = [token]
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Tokens"));

        var sut = new GenerateVersionTokenOfTCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Tokens", "AccountVersion.Generated.cs");
        Assert.False(File.Exists(generatedPath));
    }
}
