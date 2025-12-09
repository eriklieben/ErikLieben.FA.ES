using System.Runtime.InteropServices;
using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Analysis;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CLI.Tests.Analysis;

public class SolutionAnalyzerTests
{
    private static readonly List<PortableExecutableReference> References =
    [
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    ];

    private static Config CreateConfigWithDiagnostics() =>
        new() { Es = new EsConfig { EnableDiagnostics = true } };

    public class Constructor : SolutionAnalyzerTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();

            // Act
            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_performance_tracker()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var performanceTracker = Substitute.For<IPerformanceTracker>();

            // Act
            var sut = new SolutionAnalyzer(workspaceProvider, logger, config, performanceTracker);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class AnalyzeAsyncMethod : SolutionAnalyzerTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_solutionPath_is_null()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AnalyzeAsync(null!));
        }

        [Fact]
        public async Task Should_throw_ArgumentException_when_solutionPath_is_empty()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.AnalyzeAsync(string.Empty));
        }

        [Fact]
        public async Task Should_throw_ArgumentException_when_solutionPath_is_whitespace()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => sut.AnalyzeAsync("   "));
        }

        [Fact]
        public async Task Should_log_analysis_started()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var solution = CreateTestSolution();

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            await sut.AnalyzeAsync(Path.Combine(Path.GetTempPath(), "Test.sln"));

            // Assert
            Assert.Contains(logger.GetActivityLog(), e => e.Type == ActivityType.AnalysisStarted);
        }

        [Fact]
        public async Task Should_log_analysis_completed()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var solution = CreateTestSolution();

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            await sut.AnalyzeAsync(Path.Combine(Path.GetTempPath(), "Test.sln"));

            // Assert
            Assert.Contains(logger.GetActivityLog(), e => e.Type == ActivityType.AnalysisCompleted);
        }

        [Fact]
        public async Task Should_return_analysis_result_with_solution_name()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var solutionPath = Path.Combine(Path.GetTempPath(), "TestSolution.sln");
            var solution = CreateTestSolution(solutionPath);

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            var result = await sut.AnalyzeAsync(solutionPath);

            // Assert
            Assert.Equal("TestSolution", result.Solution.SolutionName);
        }

        [Fact]
        public async Task Should_return_analysis_result_with_elapsed_time()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var solution = CreateTestSolution();

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            var result = await sut.AnalyzeAsync(Path.Combine(Path.GetTempPath(), "Test.sln"));

            // Assert
            Assert.True(result.Duration > TimeSpan.Zero);
        }

        [Fact]
        public async Task Should_track_performance_when_tracker_provided()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var performanceTracker = Substitute.For<IPerformanceTracker>();
            var solution = CreateTestSolution();

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config, performanceTracker);

            // Act
            await sut.AnalyzeAsync(Path.Combine(Path.GetTempPath(), "Test.sln"));

            // Assert
            performanceTracker.Received(1).TrackAnalysis();
        }

        [Fact]
        public async Task Should_honor_cancellation_token()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.AnalyzeAsync(Path.Combine(Path.GetTempPath(), "Test.sln"), cts.Token));
        }

        [Fact]
        public async Task Should_set_generator_version_in_result()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var solution = CreateTestSolution();

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            var result = await sut.AnalyzeAsync(Path.Combine(Path.GetTempPath(), "Test.sln"));

            // Assert
            Assert.NotNull(result.Solution.Generator);
        }

        [Fact]
        public async Task Should_analyze_project_with_aggregate()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();

            var code = @"
using System;
namespace ErikLieben.FA.ES {
    public interface IEvent {}
    public interface IEventStream { }
    public interface IObjectDocument { }
    public class ObjectMetadata<T> {}
}
namespace ErikLieben.FA.ES.Processors {
    public abstract class Aggregate {
        protected Aggregate(ErikLieben.FA.ES.IEventStream s){ }
    }
}
namespace App.Domain {
    public partial class TestAggregate(ErikLieben.FA.ES.IEventStream s) : ErikLieben.FA.ES.Processors.Aggregate(s) {
        public ErikLieben.FA.ES.ObjectMetadata<Guid>? Metadata { get; private set; }
    }
}
";
            var solutionPath = Path.Combine(Path.GetTempPath(), "TestSolution.sln");
            var solution = CreateTestSolutionWithCode(solutionPath, "TestProject", code);

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            var result = await sut.AnalyzeAsync(solutionPath);

            // Assert
            Assert.Single(result.Solution.Projects);
            Assert.Single(result.Solution.Projects[0].Aggregates);
            Assert.Equal("TestAggregate", result.Solution.Projects[0].Aggregates[0].IdentifierName);
        }

        [Fact]
        public async Task Should_skip_projects_that_fail_to_compile()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();
            var solution = CreateTestSolution();

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            var result = await sut.AnalyzeAsync(Path.Combine(Path.GetTempPath(), "Test.sln"));

            // Assert - should not throw even with empty solution
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Should_log_diagnostics_when_enabled()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = CreateConfigWithDiagnostics();

            var code = "public class InvalidSyntax { "; // Missing closing brace
            var solutionPath = Path.Combine(Path.GetTempPath(), "TestSolution.sln");
            var solution = CreateTestSolutionWithCode(solutionPath, "TestProject", code);

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            await sut.AnalyzeAsync(solutionPath);

            // Assert - should have logged diagnostics
            var log = logger.GetActivityLog();
            Assert.Contains(log, e => e.Type == ActivityType.Error || e.Type == ActivityType.Warning);
        }

        [Fact]
        public async Task Should_not_add_projects_without_relevant_definitions()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();

            var code = @"
namespace App {
    public class SimpleClass { }
}
";
            var solutionPath = Path.Combine(Path.GetTempPath(), "TestSolution.sln");
            var solution = CreateTestSolutionWithCode(solutionPath, "TestProject", code);

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            var result = await sut.AnalyzeAsync(solutionPath);

            // Assert
            Assert.Empty(result.Solution.Projects);
        }

        [Fact]
        public async Task Should_skip_generated_files()
        {
            // Arrange
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();
            var logger = new SilentActivityLogger();
            var config = new Config();

            var code = @"
namespace App.Domain {
    public partial class GeneratedClass { }
}
";
            var solutionPath = Path.Combine(Path.GetTempPath(), "TestSolution.sln");
            var projectPath = Path.Combine(Path.GetTempPath(), "TestProject");
            var filePath = Path.Combine(projectPath, "Test.Generated.cs");
            var solution = CreateTestSolutionWithCode(solutionPath, "TestProject", code, filePath);

            workspaceProvider.OpenSolutionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(solution));

            var sut = new SolutionAnalyzer(workspaceProvider, logger, config);

            // Act
            var result = await sut.AnalyzeAsync(solutionPath);

            // Assert - should not have added anything since it's a generated file
            Assert.Empty(result.Solution.Projects);
        }
    }

    private static Solution CreateTestSolution(string? solutionPath = null)
    {
        solutionPath ??= Path.Combine(Path.GetTempPath(), "Test.sln");

        var workspace = new AdhocWorkspace();
        var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default, solutionPath);
        workspace.AddSolution(solutionInfo);
        return workspace.CurrentSolution;
    }

    private static Solution CreateTestSolutionWithCode(
        string solutionPath,
        string projectName,
        string code,
        string? filePath = null)
    {
        var workspace = new AdhocWorkspace();
        var projectPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, projectName);
        filePath ??= Path.Combine(projectPath, "Test.cs");

        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            projectName,
            projectName,
            LanguageNames.CSharp,
            filePath: Path.Combine(projectPath, $"{projectName}.csproj"),
            metadataReferences: References);

        var solutionInfo = SolutionInfo.Create(
            SolutionId.CreateNewId(),
            VersionStamp.Default,
            solutionPath,
            [projectInfo]);

        workspace.AddSolution(solutionInfo);

        var solution = workspace.CurrentSolution
            .AddDocument(documentId, Path.GetFileName(filePath), code, filePath: filePath);

        return solution;
    }
}
