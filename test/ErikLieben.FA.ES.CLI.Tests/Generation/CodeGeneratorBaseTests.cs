using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Generation;
using ErikLieben.FA.ES.CLI.IO;
using ErikLieben.FA.ES.CLI.Logging;
using ErikLieben.FA.ES.CLI.Model;
using NSubstitute;

namespace ErikLieben.FA.ES.CLI.Tests.Generation;

public class CodeGeneratorBaseTests
{
    private class TestCodeGenerator : CodeGeneratorBase
    {
        public override string Name => "TestGenerator";

        public TestCodeGenerator(IActivityLogger logger, ICodeWriter codeWriter, Config config)
            : base(logger, codeWriter, config)
        {
        }

        public override Task GenerateAsync(SolutionDefinition solution, string solutionPath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        // Expose protected methods for testing
        public Task<GeneratedFileResult> TestWriteCodeAsync(string filePath, string code, string? projectDirectory, CancellationToken ct = default)
            => WriteCodeAsync(filePath, code, projectDirectory, ct);

        public static string TestGetGeneratedFilePath(string solutionPath, string sourceFilePath)
            => GetGeneratedFilePath(solutionPath, sourceFilePath);

        public static string? TestGetProjectDirectory(string filePath)
            => GetProjectDirectory(filePath);
    }

    public class Constructor : CodeGeneratorBaseTests
    {
        [Fact]
        public void Should_create_instance_with_all_parameters()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            var config = new Config();

            // Act
            var sut = new TestCodeGenerator(logger, codeWriter, config);

            // Assert
            Assert.NotNull(sut);
            Assert.Equal("TestGenerator", sut.Name);
        }
    }

    public class WriteCodeAsyncMethod : CodeGeneratorBaseTests
    {
        [Fact]
        public async Task Should_call_code_writer()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            codeWriter.WriteGeneratedFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new GeneratedFileResult("/test/file.cs", true));
            var config = new Config();
            var sut = new TestCodeGenerator(logger, codeWriter, config);

            // Act
            await sut.TestWriteCodeAsync("/test/file.cs", "content", "/test");

            // Assert
            await codeWriter.Received(1).WriteGeneratedFileAsync("/test/file.cs", "content", "/test", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_result_from_code_writer()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            var expectedResult = new GeneratedFileResult("/test/file.cs", true);
            codeWriter.WriteGeneratedFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(expectedResult);
            var config = new Config();
            var sut = new TestCodeGenerator(logger, codeWriter, config);

            // Act
            var result = await sut.TestWriteCodeAsync("/test/file.cs", "content", null);

            // Assert
            Assert.True(result.Success);
            Assert.False(result.Skipped);
        }

        [Fact]
        public async Task Should_log_error_when_write_fails()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            codeWriter.WriteGeneratedFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new GeneratedFileResult("/test/file.cs", false, "Test error"));
            var config = new Config();
            var sut = new TestCodeGenerator(logger, codeWriter, config);

            // Act
            await sut.TestWriteCodeAsync("/test/file.cs", "content", null);

            // Assert
            logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Failed") && s.Contains("file.cs")), null);
        }

        [Fact]
        public async Task Should_not_log_error_when_write_succeeds()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            codeWriter.WriteGeneratedFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new GeneratedFileResult("/test/file.cs", true));
            var config = new Config();
            var sut = new TestCodeGenerator(logger, codeWriter, config);

            // Act
            await sut.TestWriteCodeAsync("/test/file.cs", "content", null);

            // Assert
            logger.DidNotReceive().LogError(Arg.Any<string>(), Arg.Any<Exception?>());
        }

        [Fact]
        public async Task Should_pass_cancellation_token()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            codeWriter.WriteGeneratedFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(new GeneratedFileResult("/test/file.cs", true));
            var config = new Config();
            var sut = new TestCodeGenerator(logger, codeWriter, config);
            using var cts = new CancellationTokenSource();

            // Act
            await sut.TestWriteCodeAsync("/test/file.cs", "content", null, cts.Token);

            // Assert
            await codeWriter.Received(1).WriteGeneratedFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), cts.Token);
        }
    }

    public class GetGeneratedFilePathMethod : CodeGeneratorBaseTests
    {
        [Fact]
        public void Should_convert_cs_file_to_generated_cs()
        {
            // Arrange
            var solutionPath = @"C:\Solution";
            var sourceFilePath = @"Project\MyClass.cs";

            // Act
            var result = TestCodeGenerator.TestGetGeneratedFilePath(solutionPath, sourceFilePath);

            // Assert
            Assert.EndsWith(".Generated.cs", result);
            Assert.Contains("MyClass", result);
        }

        [Fact]
        public void Should_handle_forward_slashes()
        {
            // Arrange
            var solutionPath = @"C:\Solution";
            var sourceFilePath = @"Project/SubFolder/MyClass.cs";

            // Act
            var result = TestCodeGenerator.TestGetGeneratedFilePath(solutionPath, sourceFilePath);

            // Assert
            Assert.Contains("MyClass.Generated.cs", result);
        }

        [Fact]
        public void Should_handle_backslashes()
        {
            // Arrange
            var solutionPath = @"C:\Solution";
            var sourceFilePath = @"Project\SubFolder\MyClass.cs";

            // Act
            var result = TestCodeGenerator.TestGetGeneratedFilePath(solutionPath, sourceFilePath);

            // Assert
            Assert.Contains("MyClass.Generated.cs", result);
        }

        [Fact]
        public void Should_append_generated_cs_when_not_cs_file()
        {
            // Arrange
            var solutionPath = @"C:\Solution";
            var sourceFilePath = @"Project\SomeFile.txt";

            // Act
            var result = TestCodeGenerator.TestGetGeneratedFilePath(solutionPath, sourceFilePath);

            // Assert
            Assert.EndsWith(".txt.Generated.cs", result);
        }

        [Fact]
        public void Should_combine_solution_path_and_relative_path()
        {
            // Arrange
            var solutionPath = @"C:\Solution";
            var sourceFilePath = @"Project\MyClass.cs";

            // Act
            var result = TestCodeGenerator.TestGetGeneratedFilePath(solutionPath, sourceFilePath);

            // Assert
            Assert.StartsWith(@"C:\Solution", result);
        }

        [Fact]
        public void Should_trim_leading_separators()
        {
            // Arrange
            var solutionPath = @"C:\Solution";
            var sourceFilePath = @"\Project\MyClass.cs";

            // Act
            var result = TestCodeGenerator.TestGetGeneratedFilePath(solutionPath, sourceFilePath);

            // Assert
            Assert.DoesNotContain(@"\\", result.Replace(@"C:\", ""));
        }

        [Fact]
        public void Should_handle_empty_source_file_path()
        {
            // Arrange
            var solutionPath = @"C:\Solution";
            var sourceFilePath = "";

            // Act
            var result = TestCodeGenerator.TestGetGeneratedFilePath(solutionPath, sourceFilePath);

            // Assert
            Assert.Equal(@"C:\Solution\.Generated.cs", result);
        }
    }

    public class GetProjectDirectoryMethod : CodeGeneratorBaseTests
    {
        [Fact]
        public void Should_find_directory_with_csproj()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var projectDir = Path.Combine(tempDir, "MyProject");
            var subDir = Path.Combine(projectDir, "src", "services");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(projectDir, "MyProject.csproj"), "<Project />");
            var testFile = Path.Combine(subDir, "Test.cs");

            try
            {
                // Act
                var result = TestCodeGenerator.TestGetProjectDirectory(testFile);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(projectDir, result);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void Should_return_null_when_no_csproj_found()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var testFile = Path.Combine(tempDir, "Test.cs");

            try
            {
                // Act
                var result = TestCodeGenerator.TestGetProjectDirectory(testFile);

                // Assert
                Assert.Null(result);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void Should_find_nearest_csproj()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var outerProject = Path.Combine(tempDir, "Outer");
            var innerProject = Path.Combine(outerProject, "Inner");
            Directory.CreateDirectory(innerProject);
            File.WriteAllText(Path.Combine(outerProject, "Outer.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(innerProject, "Inner.csproj"), "<Project />");
            var testFile = Path.Combine(innerProject, "Test.cs");

            try
            {
                // Act
                var result = TestCodeGenerator.TestGetProjectDirectory(testFile);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(innerProject, result);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
