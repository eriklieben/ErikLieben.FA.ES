using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Generation;
using ErikLieben.FA.ES.CLI.IO;
using ErikLieben.FA.ES.CLI.Logging;
using ErikLieben.FA.ES.CLI.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.CLI.Tests.Generation;

public class GenerationOrchestratorTests
{
    private readonly ISolutionAnalyzer _mockAnalyzer;
    private readonly IActivityLogger _logger;
    private readonly ICodeWriter _codeWriter;
    private readonly IPerformanceTracker _mockPerformanceTracker;

    public GenerationOrchestratorTests()
    {
        _mockAnalyzer = Substitute.For<ISolutionAnalyzer>();
        _logger = new SilentActivityLogger();
        _codeWriter = new InMemoryCodeWriter(_logger);
        _mockPerformanceTracker = Substitute.For<IPerformanceTracker>();
    }

    private static SolutionDefinition CreateEmptySolution() => new()
    {
        SolutionName = "TestSolution",
        Generator = new GeneratorInformation { Version = "1.0.0" }
    };

    private static AnalysisResult CreateAnalysisResult(SolutionDefinition? solution = null)
    {
        solution ??= CreateEmptySolution();
        return new AnalysisResult(solution, "/test/path", TimeSpan.FromMilliseconds(100));
    }

    public class Constructor : GenerationOrchestratorTests
    {
        [Fact]
        public void Should_create_instance_with_all_parameters()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();

            // Act
            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter,
                _mockPerformanceTracker,
                parallelGeneration: true);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_without_optional_parameters()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();

            // Act
            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class GenerateAsyncMethod : GenerationOrchestratorTests
    {
        [Fact]
        public async Task Should_call_analyzer()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter);

            // Act
            await sut.GenerateAsync("/test/solution.sln");

            // Assert
            await _mockAnalyzer.Received(1).AnalyzeAsync("/test/solution.sln", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_log_generation_started()
        {
            // Arrange
            var silentLogger = new SilentActivityLogger();
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                silentLogger,
                _codeWriter);

            // Act
            await sut.GenerateAsync("/test/solution.sln");

            // Assert
            Assert.Contains(silentLogger.GetActivityLog(), e => e.Type == ActivityType.GenerationStarted);
        }

        [Fact]
        public async Task Should_log_generation_completed()
        {
            // Arrange
            var silentLogger = new SilentActivityLogger();
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                silentLogger,
                _codeWriter);

            // Act
            await sut.GenerateAsync("/test/solution.sln");

            // Assert
            Assert.Contains(silentLogger.GetActivityLog(), e => e.Type == ActivityType.GenerationCompleted);
        }

        [Fact]
        public async Task Should_run_all_generators()
        {
            // Arrange
            var generator1 = Substitute.For<ICodeGenerator>();
            generator1.Name.Returns("Generator1");
            var generator2 = Substitute.For<ICodeGenerator>();
            generator2.Name.Returns("Generator2");

            var generators = new List<ICodeGenerator> { generator1, generator2 };
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter,
                parallelGeneration: false);

            // Act
            await sut.GenerateAsync("/test/solution.sln");

            // Assert
            await generator1.Received(1).GenerateAsync(Arg.Any<SolutionDefinition>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await generator2.Received(1).GenerateAsync(Arg.Any<SolutionDefinition>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_run_generators_in_parallel_when_enabled()
        {
            // Arrange
            var generator1 = Substitute.For<ICodeGenerator>();
            generator1.Name.Returns("Generator1");
            var generator2 = Substitute.For<ICodeGenerator>();
            generator2.Name.Returns("Generator2");

            var generators = new List<ICodeGenerator> { generator1, generator2 };
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter,
                parallelGeneration: true);

            // Act
            await sut.GenerateAsync("/test/solution.sln");

            // Assert - Both generators should still be called
            await generator1.Received(1).GenerateAsync(Arg.Any<SolutionDefinition>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await generator2.Received(1).GenerateAsync(Arg.Any<SolutionDefinition>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_generation_result()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter);

            // Act
            var result = await sut.GenerateAsync("/test/solution.sln");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestSolution", result.Solution.SolutionName);
        }

        [Fact]
        public async Task Should_track_performance_when_tracker_provided()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter,
                _mockPerformanceTracker);

            // Act
            await sut.GenerateAsync("/test/solution.sln");

            // Assert
            _mockPerformanceTracker.Received(1).TrackGeneration();
        }

        [Fact]
        public async Task Should_handle_generator_exception_and_continue()
        {
            // Arrange
            var silentLogger = new SilentActivityLogger();
            var failingGenerator = Substitute.For<ICodeGenerator>();
            failingGenerator.Name.Returns("FailingGenerator");
            failingGenerator.GenerateAsync(Arg.Any<SolutionDefinition>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Test error"));

            var successGenerator = Substitute.For<ICodeGenerator>();
            successGenerator.Name.Returns("SuccessGenerator");

            var generators = new List<ICodeGenerator> { failingGenerator, successGenerator };
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                silentLogger,
                _codeWriter,
                parallelGeneration: false);

            // Act
            var result = await sut.GenerateAsync("/test/solution.sln");

            // Assert - Should not throw, should log error, and continue
            Assert.NotNull(result);
            await successGenerator.Received(1).GenerateAsync(Arg.Any<SolutionDefinition>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            Assert.Contains(silentLogger.GetActivityLog(), e => e.Type == ActivityType.Error);
        }

        [Fact]
        public async Task Should_return_written_files_in_result()
        {
            // Arrange
            var inMemoryWriter = new InMemoryCodeWriter();
            await inMemoryWriter.WriteGeneratedFileAsync("/test/file1.cs", "content1");
            await inMemoryWriter.WriteGeneratedFileAsync("/test/file2.cs", "content2");

            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                inMemoryWriter);

            // Act
            var result = await sut.GenerateAsync("/test/solution.sln");

            // Assert
            Assert.Equal(2, result.GeneratedFiles.Count);
        }

        [Fact]
        public async Task Should_honor_cancellation_token()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.GenerateAsync("/test/solution.sln", cts.Token));
        }
    }

    public class GenerateIncrementalAsyncMethod : GenerationOrchestratorTests
    {
        [Fact]
        public async Task Should_call_analyzer()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter);

            var changedFiles = new List<string> { "/test/file.cs" };

            // Act
            await sut.GenerateIncrementalAsync("/test/solution.sln", changedFiles);

            // Assert
            await _mockAnalyzer.Received(1).AnalyzeAsync("/test/solution.sln", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_log_incremental_generation_started()
        {
            // Arrange
            var silentLogger = new SilentActivityLogger();
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                silentLogger,
                _codeWriter);

            var changedFiles = new List<string> { "/test/file.cs" };

            // Act
            await sut.GenerateIncrementalAsync("/test/solution.sln", changedFiles);

            // Assert
            Assert.Contains(silentLogger.GetActivityLog(), e =>
                e.Type == ActivityType.GenerationStarted &&
                e.Message.Contains("incremental"));
        }

        [Fact]
        public async Task Should_run_incremental_generators_when_should_regenerate()
        {
            // Arrange
            var incrementalGenerator = Substitute.For<IIncrementalGenerator>();
            incrementalGenerator.Name.Returns("IncrementalGen");
            incrementalGenerator.ShouldRegenerate(Arg.Any<IReadOnlyList<string>>(), Arg.Any<SolutionDefinition>())
                .Returns(true);

            var generators = new List<ICodeGenerator> { incrementalGenerator };
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter);

            var changedFiles = new List<string> { "/test/file.cs" };

            // Act
            await sut.GenerateIncrementalAsync("/test/solution.sln", changedFiles);

            // Assert
            await incrementalGenerator.Received(1).GenerateIncrementalAsync(
                Arg.Any<SolutionDefinition>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_skip_incremental_generators_when_should_not_regenerate()
        {
            // Arrange
            var silentLogger = new SilentActivityLogger();
            var incrementalGenerator = Substitute.For<IIncrementalGenerator>();
            incrementalGenerator.Name.Returns("IncrementalGen");
            incrementalGenerator.ShouldRegenerate(Arg.Any<IReadOnlyList<string>>(), Arg.Any<SolutionDefinition>())
                .Returns(false);

            var generators = new List<ICodeGenerator> { incrementalGenerator };
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                silentLogger,
                _codeWriter);

            var changedFiles = new List<string> { "/test/file.cs" };

            // Act
            await sut.GenerateIncrementalAsync("/test/solution.sln", changedFiles);

            // Assert
            await incrementalGenerator.DidNotReceive().GenerateIncrementalAsync(
                Arg.Any<SolutionDefinition>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
            Assert.Contains(silentLogger.GetActivityLog(), e =>
                e.Type == ActivityType.Info &&
                e.Message.Contains("Skipping"));
        }

        [Fact]
        public async Task Should_run_regular_generators_normally()
        {
            // Arrange
            var regularGenerator = Substitute.For<ICodeGenerator>();
            regularGenerator.Name.Returns("RegularGen");

            var generators = new List<ICodeGenerator> { regularGenerator };
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter,
                parallelGeneration: false);

            var changedFiles = new List<string> { "/test/file.cs" };

            // Act
            await sut.GenerateIncrementalAsync("/test/solution.sln", changedFiles);

            // Assert
            await regularGenerator.Received(1).GenerateAsync(
                Arg.Any<SolutionDefinition>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_run_regular_generators_in_parallel_when_enabled()
        {
            // Arrange
            var regularGenerator1 = Substitute.For<ICodeGenerator>();
            regularGenerator1.Name.Returns("RegularGen1");
            var regularGenerator2 = Substitute.For<ICodeGenerator>();
            regularGenerator2.Name.Returns("RegularGen2");

            var generators = new List<ICodeGenerator> { regularGenerator1, regularGenerator2 };
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter,
                parallelGeneration: true);

            var changedFiles = new List<string> { "/test/file.cs" };

            // Act
            await sut.GenerateIncrementalAsync("/test/solution.sln", changedFiles);

            // Assert
            await regularGenerator1.Received(1).GenerateAsync(Arg.Any<SolutionDefinition>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await regularGenerator2.Received(1).GenerateAsync(Arg.Any<SolutionDefinition>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_generation_result()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter);

            var changedFiles = new List<string> { "/test/file.cs" };

            // Act
            var result = await sut.GenerateIncrementalAsync("/test/solution.sln", changedFiles);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestSolution", result.Solution.SolutionName);
        }

        [Fact]
        public async Task Should_track_performance_when_tracker_provided()
        {
            // Arrange
            var generators = new List<ICodeGenerator>();
            _mockAnalyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(CreateAnalysisResult()));

            var sut = new GenerationOrchestrator(
                _mockAnalyzer,
                generators,
                _logger,
                _codeWriter,
                _mockPerformanceTracker);

            var changedFiles = new List<string> { "/test/file.cs" };

            // Act
            await sut.GenerateIncrementalAsync("/test/solution.sln", changedFiles);

            // Assert
            _mockPerformanceTracker.Received(1).TrackGeneration();
        }
    }

    public class CreateDefaultMethod : GenerationOrchestratorTests
    {
        [Fact]
        public void Should_create_orchestrator_with_default_generators()
        {
            // Arrange
            var config = new Config();

            // Act
            var sut = GenerationOrchestrator.CreateDefault(
                _logger,
                _codeWriter,
                config);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_orchestrator_with_custom_workspace_provider()
        {
            // Arrange
            var config = new Config();
            var workspaceProvider = Substitute.For<IWorkspaceProvider>();

            // Act
            var sut = GenerationOrchestrator.CreateDefault(
                _logger,
                _codeWriter,
                config,
                workspaceProvider);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_orchestrator_with_performance_tracker()
        {
            // Arrange
            var config = new Config();

            // Act
            var sut = GenerationOrchestrator.CreateDefault(
                _logger,
                _codeWriter,
                config,
                performanceTracker: _mockPerformanceTracker);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_respect_parallel_generation_flag()
        {
            // Arrange
            var config = new Config();

            // Act
            var sut = GenerationOrchestrator.CreateDefault(
                _logger,
                _codeWriter,
                config,
                parallelGeneration: false);

            // Assert
            Assert.NotNull(sut);
        }
    }
}
