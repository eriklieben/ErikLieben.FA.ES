using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.IO;
using ErikLieben.FA.ES.CLI.Logging;

namespace ErikLieben.FA.ES.CLI.Tests.IO;

public class InMemoryCodeWriterTests
{
    public class Constructor : InMemoryCodeWriterTests
    {
        [Fact]
        public void Should_create_instance_without_logger()
        {
            // Act
            var sut = new InMemoryCodeWriter();

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_logger()
        {
            // Arrange
            var logger = new SilentActivityLogger();

            // Act
            var sut = new InMemoryCodeWriter(logger);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class WriteGeneratedFileAsyncMethod : InMemoryCodeWriterTests
    {
        [Fact]
        public async Task Should_write_file_to_memory()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            var path = "/test/file.cs";
            var content = "public class Test {}";

            // Act
            var result = await sut.WriteGeneratedFileAsync(path, content);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(content, result.GeneratedContent);
            Assert.False(result.Skipped);
        }

        [Fact]
        public async Task Should_normalize_path()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            var path = @"C:\test\file.cs";
            var content = "content";

            // Act
            await sut.WriteGeneratedFileAsync(path, content);

            // Assert
            Assert.True(sut.HasFile(@"c:/test/file.cs"));
        }

        [Fact]
        public async Task Should_log_file_generation_when_logger_provided()
        {
            // Arrange
            var logger = new SilentActivityLogger();
            var sut = new InMemoryCodeWriter(logger);

            // Act
            await sut.WriteGeneratedFileAsync("/test/file.cs", "content");

            // Assert
            var log = logger.GetActivityLog();
            Assert.Contains(log, e => e.Type == ActivityType.FileGenerated);
        }

        [Fact]
        public async Task Should_overwrite_existing_file()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            var path = "/test/file.cs";

            // Act
            await sut.WriteGeneratedFileAsync(path, "content1");
            await sut.WriteGeneratedFileAsync(path, "content2");

            // Assert
            Assert.Equal("content2", sut.GetFileContent(path));
        }

        [Fact]
        public async Task Should_ignore_project_directory_parameter()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();

            // Act
            var result = await sut.WriteGeneratedFileAsync("/test/file.cs", "content", "/project");

            // Assert
            Assert.True(result.Success);
        }
    }

    public class GetWrittenFilesMethod : InMemoryCodeWriterTests
    {
        [Fact]
        public void Should_return_empty_list_for_fresh_writer()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();

            // Act
            var result = sut.GetWrittenFiles();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_all_written_files()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            await sut.WriteGeneratedFileAsync("/test/file1.cs", "content1");
            await sut.WriteGeneratedFileAsync("/test/file2.cs", "content2");

            // Act
            var result = sut.GetWrittenFiles();

            // Assert
            Assert.Equal(2, result.Count);
        }
    }

    public class ClearMethod : InMemoryCodeWriterTests
    {
        [Fact]
        public async Task Should_clear_all_files()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            await sut.WriteGeneratedFileAsync("/test/file1.cs", "content1");
            await sut.WriteGeneratedFileAsync("/test/file2.cs", "content2");

            // Act
            sut.Clear();

            // Assert
            Assert.Empty(sut.GetWrittenFiles());
        }
    }

    public class HasFileMethod : InMemoryCodeWriterTests
    {
        [Fact]
        public void Should_return_false_when_file_not_exists()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();

            // Act
            var result = sut.HasFile("/nonexistent.cs");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_true_when_file_exists()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            await sut.WriteGeneratedFileAsync("/test/file.cs", "content");

            // Act
            var result = sut.HasFile("/test/file.cs");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_normalize_path_when_checking()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            await sut.WriteGeneratedFileAsync(@"C:\test\file.cs", "content");

            // Act
            var result = sut.HasFile(@"c:/test/file.cs");

            // Assert
            Assert.True(result);
        }
    }

    public class GetFileContentMethod : InMemoryCodeWriterTests
    {
        [Fact]
        public void Should_return_null_when_file_not_exists()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();

            // Act
            var result = sut.GetFileContent("/nonexistent.cs");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_content_when_file_exists()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            await sut.WriteGeneratedFileAsync("/test/file.cs", "expected content");

            // Act
            var result = sut.GetFileContent("/test/file.cs");

            // Assert
            Assert.Equal("expected content", result);
        }
    }

    public class GetFilePathsMethod : InMemoryCodeWriterTests
    {
        [Fact]
        public void Should_return_empty_for_fresh_writer()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();

            // Act
            var result = sut.GetFilePaths();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task Should_return_all_file_paths()
        {
            // Arrange
            var sut = new InMemoryCodeWriter();
            await sut.WriteGeneratedFileAsync("/test/file1.cs", "content1");
            await sut.WriteGeneratedFileAsync("/test/file2.cs", "content2");

            // Act
            var result = sut.GetFilePaths().ToList();

            // Assert
            Assert.Equal(2, result.Count);
        }
    }
}
