using ErikLieben.FA.ES.CLI.Configuration;
using Spectre.Console.Testing;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze;

public class AnalyzeTests
{
    public class AnalyzeAsync
    {
        [Fact]
        public async Task Should_throw_exception_when_solutionPath_is_null()
        {
            // Arrange
            var config = new Config();
            var console = new TestConsole();
            var sut = new CLI.Analyze.Analyze(config, console);

            // Act
            var result =
                await Assert.ThrowsAsync<ArgumentNullException>(
                    async () =>  await sut.AnalyzeAsync(null!));

            // Assert
            Assert.Equal("solutionPath", result.ParamName);
        }

        [Fact]
        public async Task Should_throw_exception_when_solutionPath_is_empty()
        {
            // Arrange
            var config = new Config();
            var console = new TestConsole();
            var sut = new CLI.Analyze.Analyze(config, console);

            // Act
            var result =
                await Assert.ThrowsAsync<ArgumentException>(
                    async () =>  await sut.AnalyzeAsync(""));

            // Assert
            Assert.Equal("solutionPath", result.ParamName);
        }

        [Fact]
        public async Task Should_throw_exception_when_solutionPath_is_empty_space()
        {
            // Arrange
            var config = new Config();
            var console = new TestConsole();
            var sut = new CLI.Analyze.Analyze(config, console);

            // Act
            var result =
                await Assert.ThrowsAsync<ArgumentException>(
                    async () =>  await sut.AnalyzeAsync(" "));

            // Assert
            Assert.Equal("solutionPath", result.ParamName);
        }

        [Fact]
        public async Task Should_analyze_minimal_solution_and_return_empty_projects()
        {
            // Arrange: create temp solution and project
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var slnPath = Path.Combine(root, "App.sln");
            var projDir = Path.Combine(root, "App");
            Directory.CreateDirectory(projDir);
            var projGuid = Guid.NewGuid().ToString().ToUpper();
            var csprojPath = Path.Combine(projDir, "App.csproj");

            await File.WriteAllTextAsync(csprojPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");
            await File.WriteAllTextAsync(Path.Combine(projDir, "Class1.cs"), "namespace Tmp; public class Class1 {} ");

            // Minimal .sln content
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
            await File.WriteAllTextAsync(slnPath, sln);

            var config = new Config();
            var console = new TestConsole();
            var sut = new CLI.Analyze.Analyze(config, console);

            // Act
            var (solutionDef, rootPath) = await sut.AnalyzeAsync(slnPath);

            try
            {
                // Assert
                Assert.Equal("App", solutionDef.SolutionName);
                Assert.Empty(solutionDef.Projects);
                Assert.True(rootPath.Replace('\\','/').EndsWith("/" + new DirectoryInfo(root).Name));
            }
            finally
            {
                // Cleanup
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }

    public class PrivateHelpers
    {
        [Fact]
        public async Task Should_count_classes_via_private_method()
        {
            // Arrange: build an AdhocWorkspace Solution with 2 class declarations
            var workspace = new AdhocWorkspace(MefHostServices.DefaultHost);
            var project = workspace.CurrentSolution.AddProject("P", "P", LanguageNames.CSharp);
            project = project.AddDocument("A.cs", SourceText.From("public class A {} public class B {}")).Project;
            var solution = project.Solution;

            var config = new Config();
            var console = new TestConsole();
            var sut = new CLI.Analyze.Analyze(config, console);

            // Reflect CountClassDeclarationsAsync
            var mi = typeof(CLI.Analyze.Analyze).GetMethod("CountClassDeclarationsAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mi);
            var task = (Task<int>)mi!.Invoke(sut, new object[] { solution })!;
            var result = await task;

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public void Should_log_compilation_issues_via_private_logger()
        {
            // Arrange: create a compilation with a syntax error
            var tree = CSharpSyntaxTree.ParseText("public class {");
            var compilation = CSharpCompilation.Create("Err", new[] { tree }, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Reflect LogCompilationIssues
            var mi = typeof(CLI.Analyze.Analyze).GetMethod("LogCompilationIssues", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);

            // Act & Assert: should not throw
            mi!.Invoke(null, new object[] { compilation });
        }

        [Fact]
        public void Should_get_generator_version_non_null()
        {
            var mi = typeof(CLI.Analyze.Analyze).GetMethod("GetGeneratorVersion", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(mi);
            var version = (string?)mi!.Invoke(null, Array.Empty<object>());
            Assert.NotNull(version);
        }
    }
}
