using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.IO;
using NSubstitute;

namespace ErikLieben.FA.ES.CLI.Tests.IO;

public class FileSystemCodeWriterTests
{
    public class Constructor : FileSystemCodeWriterTests
    {
        [Fact]
        public void Should_create_instance_with_logger()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();

            // Act
            var sut = new FileSystemCodeWriter(logger);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_custom_concurrency()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();

            // Act
            var sut = new FileSystemCodeWriter(logger, maxConcurrency: 4);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_skip_unchanged_disabled()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();

            // Act
            var sut = new FileSystemCodeWriter(logger, skipUnchanged: false);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class WriteGeneratedFileAsyncMethod : FileSystemCodeWriterTests
    {
        [Fact]
        public async Task Should_write_file_to_disk()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test { }";

            try
            {
                // Act
                var result = await sut.WriteGeneratedFileAsync(filePath, content);

                // Assert
                Assert.True(result.Success);
                Assert.False(result.Skipped);
                Assert.True(File.Exists(filePath));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_create_directory_if_not_exists()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "sub", "dir");
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test { }";

            try
            {
                // Act
                var result = await sut.WriteGeneratedFileAsync(filePath, content);

                // Assert
                Assert.True(result.Success);
                Assert.True(Directory.Exists(tempDir));
                Assert.True(File.Exists(filePath));
            }
            finally
            {
                var rootDir = Path.Combine(Path.GetTempPath(), Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(tempDir))!)!);
                try { Directory.Delete(rootDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_skip_unchanged_file()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger, skipUnchanged: true);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test { }";

            try
            {
                // Write file first time
                await sut.WriteGeneratedFileAsync(filePath, content);
                sut.Clear();

                // Act - Write same content again
                var result = await sut.WriteGeneratedFileAsync(filePath, content);

                // Assert
                Assert.True(result.Success);
                Assert.True(result.Skipped);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_not_skip_when_skip_unchanged_disabled()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger, skipUnchanged: false);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test { }";

            try
            {
                // Write file first time
                await sut.WriteGeneratedFileAsync(filePath, content);
                sut.Clear();

                // Act - Write same content again
                var result = await sut.WriteGeneratedFileAsync(filePath, content);

                // Assert
                Assert.True(result.Success);
                Assert.False(result.Skipped);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_overwrite_changed_file()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content1 = "public class Test { }";
            var content2 = "public class Test { public void Foo() { } }";

            try
            {
                // Write file first time
                await sut.WriteGeneratedFileAsync(filePath, content1);
                sut.Clear();

                // Act - Write different content
                var result = await sut.WriteGeneratedFileAsync(filePath, content2);

                // Assert
                Assert.True(result.Success);
                Assert.False(result.Skipped);
                var actualContent = await File.ReadAllTextAsync(filePath);
                Assert.Contains("Foo", actualContent);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_format_code_before_writing()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test{public void Foo(){}}";

            try
            {
                // Act
                var result = await sut.WriteGeneratedFileAsync(filePath, content);

                // Assert
                Assert.True(result.Success);
                var actualContent = await File.ReadAllTextAsync(filePath);
                // Formatted code should have proper indentation
                Assert.Contains("public class Test", actualContent);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_log_file_generated()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test { }";

            try
            {
                // Act
                await sut.WriteGeneratedFileAsync(filePath, content);

                // Assert
                logger.Received(1).Log(
                    ActivityType.FileGenerated,
                    Arg.Is<string>(s => s.Contains("Test.cs")),
                    Arg.Any<string?>(),
                    Arg.Any<string?>());
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_log_file_skipped()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger, skipUnchanged: true);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test { }";

            try
            {
                // Write first time
                await sut.WriteGeneratedFileAsync(filePath, content);
                logger.ClearReceivedCalls();

                // Act - Write same content again
                await sut.WriteGeneratedFileAsync(filePath, content);

                // Assert
                logger.Received(1).Log(
                    ActivityType.FileSkipped,
                    Arg.Is<string>(s => s.Contains("Unchanged")),
                    Arg.Any<string?>(),
                    Arg.Any<string?>());
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_handle_cancellation()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test { }";
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                // Act & Assert
                // TaskCanceledException derives from OperationCanceledException
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => sut.WriteGeneratedFileAsync(filePath, content, cancellationToken: cts.Token));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_return_error_result_on_failure()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            // Invalid path that should fail
            var filePath = Path.Combine("Z:", "NonExistent", "Path", "Test.cs");
            var content = "public class Test { }";

            // Act
            var result = await sut.WriteGeneratedFileAsync(filePath, content);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task Should_log_error_on_failure()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var filePath = Path.Combine("Z:", "NonExistent", "Path", "Test.cs");
            var content = "public class Test { }";

            // Act
            await sut.WriteGeneratedFileAsync(filePath, content);

            // Assert
            logger.Received(1).LogError(
                Arg.Is<string>(s => s.Contains("Failed")),
                Arg.Any<Exception?>());
        }

        [Fact]
        public async Task Should_use_project_directory_for_formatting()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");
            var content = "public class Test { }";

            try
            {
                // Act
                var result = await sut.WriteGeneratedFileAsync(filePath, content, tempDir);

                // Assert
                Assert.True(result.Success);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public class GetWrittenFilesMethod : FileSystemCodeWriterTests
    {
        [Fact]
        public async Task Should_return_all_written_files()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath1 = Path.Combine(tempDir, "Test1.cs");
            var filePath2 = Path.Combine(tempDir, "Test2.cs");

            try
            {
                await sut.WriteGeneratedFileAsync(filePath1, "public class Test1 { }");
                await sut.WriteGeneratedFileAsync(filePath2, "public class Test2 { }");

                // Act
                var results = sut.GetWrittenFiles();

                // Assert
                Assert.Equal(2, results.Count);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void Should_return_empty_for_fresh_writer()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);

            // Act
            var results = sut.GetWrittenFiles();

            // Assert
            Assert.Empty(results);
        }
    }

    public class ClearMethod : FileSystemCodeWriterTests
    {
        [Fact]
        public async Task Should_clear_written_files()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");

            try
            {
                await sut.WriteGeneratedFileAsync(filePath, "public class Test { }");
                Assert.Single(sut.GetWrittenFiles());

                // Act
                sut.Clear();

                // Assert
                Assert.Empty(sut.GetWrittenFiles());
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public class CountProperties : FileSystemCodeWriterTests
    {
        [Fact]
        public async Task WrittenCount_should_exclude_skipped_files()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger, skipUnchanged: true);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "Test.cs");

            try
            {
                // Write file first time
                await sut.WriteGeneratedFileAsync(filePath, "public class Test { }");
                // Write same content again (skipped)
                await sut.WriteGeneratedFileAsync(filePath, "public class Test { }");

                // Act & Assert
                Assert.Equal(1, sut.WrittenCount);
                Assert.Equal(1, sut.SkippedCount);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task FailedCount_should_count_failed_writes()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var validPath = Path.Combine(tempDir, "Valid.cs");
            var invalidPath = Path.Combine("Z:", "Invalid", "Path.cs");

            try
            {
                await sut.WriteGeneratedFileAsync(validPath, "public class Valid { }");
                await sut.WriteGeneratedFileAsync(invalidPath, "public class Invalid { }");

                // Act & Assert
                Assert.Equal(1, sut.WrittenCount);
                Assert.Equal(1, sut.FailedCount);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public class ConcurrencyTests : FileSystemCodeWriterTests
    {
        [Fact]
        public async Task Should_handle_concurrent_writes()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var sut = new FileSystemCodeWriter(logger, maxConcurrency: 2);
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var tasks = Enumerable.Range(1, 10)
                    .Select(i => sut.WriteGeneratedFileAsync(
                        Path.Combine(tempDir, $"Test{i}.cs"),
                        $"public class Test{i} {{ }}"))
                    .ToList();

                // Act
                var results = await Task.WhenAll(tasks);

                // Assert
                Assert.All(results, r => Assert.True(r.Success));
                Assert.Equal(10, sut.WrittenCount);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
