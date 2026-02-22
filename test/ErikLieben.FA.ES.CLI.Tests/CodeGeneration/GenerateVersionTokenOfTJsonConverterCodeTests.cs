using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.CodeGeneration;

public class GenerateVersionTokenOfTJsonConverterCodeTests
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
    public async Task Generate_writes_converter_with_type_mapping_for_version_tokens()
    {
        // Arrange: two version tokens so the switch gets multiple cases
        var tokens = new List<VersionTokenDefinition>
        {
            new VersionTokenDefinition
            {
                Name = "OrderVersion",
                Namespace = "Demo.App.Tokens",
                GenericType = "Guid",
                NamespaceOfType = "System",
                IsPartialClass = true,
                FileLocations = ["Demo\\Tokens\\OrderVersion.cs"]
            },
            new VersionTokenDefinition
            {
                Name = "AccountVersion",
                Namespace = "Demo.App.Tokens",
                GenericType = "String",
                NamespaceOfType = "System",
                IsPartialClass = true,
                FileLocations = ["Demo\\Tokens\\AccountVersion.cs"]
            }
        };

        var converter = new VersionTokenJsonConverterDefinition
        {
            Name = "DemoJsonConverter",
            Namespace = "Demo.App.Json",
            IsPartialClass = true,
            FileLocations = ["Demo\\Serialization\\DemoJsonConverter.cs"]
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            VersionTokens = tokens,
            VersionTokenJsonConverterDefinitions = [converter]
        };

        var (solution, outDir) = BuildSolution(project);
        // Generator does not create directories -> pre-create
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Serialization"));

        var sut = new GenerateVersionTokenOfTJsonConverterCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Serialization", "DemoJsonConverter.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Using directives
        Assert.Contains("using System.Text.Json;", code);
        Assert.Contains("using ErikLieben.FA.ES;", code);
        Assert.Contains("using System;", code); // from NamespaceOfType of Guid/String

        // Class and Read method
        Assert.Contains("namespace Demo.App.Json;", code);
        Assert.Contains("public partial class DemoJsonConverter<T>", code);
        Assert.Contains("public override partial T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)", code);
        Assert.Contains("Converter.Read(ref reader, typeof(VersionToken), options)", code);

        // Switch mappings for both tokens
        Assert.Contains("when typeof(T) == typeof(OrderVersion) => new OrderVersion(versionToken.Value) as T", code);
        Assert.Contains("when typeof(T) == typeof(AccountVersion) => new AccountVersion(versionToken.Value) as T", code);

        // Default case to null
        Assert.Contains("_ => null", code);
    }

    [Fact]
    public async Task Generate_skips_when_converter_is_not_partial()
    {
        // Arrange
        var tokens = new List<VersionTokenDefinition>
        {
            new VersionTokenDefinition
            {
                Name = "OrderVersion",
                Namespace = "Demo.App.Tokens",
                GenericType = "Guid",
                NamespaceOfType = "System",
                IsPartialClass = true,
                FileLocations = ["Demo\\Tokens\\OrderVersion.cs"]
            }
        };

        var converter = new VersionTokenJsonConverterDefinition
        {
            Name = "NotPartialConverter",
            Namespace = "Demo.App.Json",
            IsPartialClass = false,
            FileLocations = ["Demo\\Serialization\\NotPartialConverter.cs"]
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            VersionTokens = tokens,
            VersionTokenJsonConverterDefinitions = [converter]
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Serialization"));

        var sut = new GenerateVersionTokenOfTJsonConverterCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert: no file generated for non-partial converter
        var generatedPath = Path.Combine(outDir, "Demo", "Serialization", "NotPartialConverter.Generated.cs");
        Assert.False(File.Exists(generatedPath));
    }
}
