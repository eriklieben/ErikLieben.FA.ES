using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.Commands;
using Spectre.Console.Cli;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Commands;

public class GenerateCommandTests
{
    private static string CreateMinimalSolution()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var slnPath = Path.Combine(root, "App.sln");
        var projDir = Path.Combine(root, "App");
        Directory.CreateDirectory(projDir);
        var projGuid = Guid.NewGuid().ToString().ToUpper();
        var csprojPath = Path.Combine(projDir, "App.csproj");

        File.WriteAllText(csprojPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");
        File.WriteAllText(Path.Combine(projDir, "Class1.cs"), "namespace Tmp; public class Class1 {} ");

        var slnTemplate = """
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31912.275
MinimumVisualStudioVersion = 10.0.40219.1
Project("{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}") = "App", "App\App.csproj", "{PROJGUID}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{PROJGUID}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{PROJGUID}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{PROJGUID}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{PROJGUID}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
""";
        var sln = slnTemplate.Replace("{PROJGUID}", "{" + projGuid + "}");
        File.WriteAllText(slnPath, sln);
        return slnPath;
    }

    [Fact]
    public async Task ExecuteAsync_should_return_1_when_no_solution_found()
    {
        // Arrange
        var sut = new GenerateCommand();
        var settings = new GenerateCommand.Settings { Path = null };
        var originalCwd = Directory.GetCurrentDirectory();
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Directory.SetCurrentDirectory(temp);
        var originalEnv = Environment.GetEnvironmentVariable("ELFA_SKIP_DEBUG_PATH");
        Environment.SetEnvironmentVariable("ELFA_SKIP_DEBUG_PATH", "1");

        try
        {
            // Act
            var result = await sut.ExecuteAsync(context: null!, settings);

            // Assert
            Assert.Equal(1, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            Environment.SetEnvironmentVariable("ELFA_SKIP_DEBUG_PATH", originalEnv);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public async Task ExecuteAsync_should_process_minimal_solution_and_write_analyzed_data()
    {
        // Arrange
        var slnPath = CreateMinimalSolution();
        var sut = new GenerateCommand();
        var settings = new GenerateCommand.Settings { Path = slnPath, WithDiff = false };

        try
        {
            // Act
            var result = await sut.ExecuteAsync(context: null!, settings);

            // Assert
            Assert.Equal(0, result);
            var expectedAnalyzePath = Path.Combine(Path.GetDirectoryName(slnPath)!, ".elfa\\","eriklieben.fa.es.analyzed-data.json");
            Assert.True(File.Exists(expectedAnalyzePath));

            var content = await File.ReadAllTextAsync(expectedAnalyzePath);
            Assert.False(string.IsNullOrWhiteSpace(content));
            Assert.Contains("\"SolutionName\"", content);
        }
        finally
        {
            // Cleanup temp solution folder
            try { Directory.Delete(Path.GetDirectoryName(slnPath)!, true); } catch { }
        }
    }

    [Fact]
    public void FindSolutionFile_should_find_sln_in_subdirectories()
    {
        // Arrange
        var originalCwd = Directory.GetCurrentDirectory();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(root, "sub");
        Directory.CreateDirectory(sub);
        var sln = Path.Combine(sub, "App.sln");
        File.WriteAllText(sln, "");
        Directory.SetCurrentDirectory(root);

        try
        {
            var sut = new GenerateCommand();
            var mi = typeof(GenerateCommand).GetMethod("FindSolutionFile", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(mi);

            // Act
            var result = (string?)mi!.Invoke(sut, Array.Empty<object>());

            // Assert
            Assert.NotNull(result);
            Assert.True(result!.EndsWith("App.sln", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
