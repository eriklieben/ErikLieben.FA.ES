using System.Reflection;
using ErikLieben.FA.ES.CLI.Commands;

namespace ErikLieben.FA.ES.CLI.Tests.Commands;

public class WatchCommandTests
{
    public class Settings : WatchCommandTests
    {
        [Fact]
        public void Should_have_default_values()
        {
            // Arrange & Act
            var settings = new WatchCommand.Settings();

            // Assert
            Assert.Null(settings.Path);
            Assert.False(settings.Verbose);
            Assert.False(settings.Simple);
        }

        [Fact]
        public void Should_allow_setting_path()
        {
            // Arrange & Act
            var settings = new WatchCommand.Settings { Path = "/test/solution.sln" };

            // Assert
            Assert.Equal("/test/solution.sln", settings.Path);
        }

        [Fact]
        public void Should_allow_setting_verbose()
        {
            // Arrange & Act
            var settings = new WatchCommand.Settings { Verbose = true };

            // Assert
            Assert.True(settings.Verbose);
        }

        [Fact]
        public void Should_allow_setting_simple()
        {
            // Arrange & Act
            var settings = new WatchCommand.Settings { Simple = true };

            // Assert
            Assert.True(settings.Simple);
        }
    }

    public class ResolveSolutionPathMethod : WatchCommandTests
    {
        [Fact]
        public void Should_return_provided_path()
        {
            // Arrange
            var settings = new WatchCommand.Settings { Path = "/test/solution.sln" };
            var method = typeof(WatchCommand).GetMethod("ResolveSolutionPath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            // Act
            var result = method!.Invoke(null, [settings]);

            // Assert
            Assert.Equal("/test/solution.sln", result);
        }

        [Fact]
        public void Should_find_solution_in_current_directory()
        {
            // Arrange
            var originalCwd = Directory.GetCurrentDirectory();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var slnPath = Path.Combine(tempDir, "Test.sln");
            File.WriteAllText(slnPath, "");

            try
            {
                Directory.SetCurrentDirectory(tempDir);
                var method = typeof(WatchCommand).GetMethod("FindSolutionFile", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var result = (string?)method!.Invoke(null, []);

                // Assert
                Assert.NotNull(result);
                Assert.EndsWith("Test.sln", result!, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void Should_return_null_when_no_solution_found()
        {
            // Arrange
            var originalCwd = Directory.GetCurrentDirectory();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                Directory.SetCurrentDirectory(tempDir);
                var method = typeof(WatchCommand).GetMethod("FindSolutionFile", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var result = (string?)method!.Invoke(null, []);

                // Assert
                Assert.Null(result);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public class ShouldIgnorePathMethod : WatchCommandTests
    {
        [Theory]
        [InlineData("/project/bin/Debug/file.cs", true)]
        [InlineData("/project/obj/Debug/file.cs", true)]
        [InlineData("/project/src/file.cs", false)]
        [InlineData("/project/.elfa/data.json", true)]
        public void Should_correctly_identify_paths_to_ignore(string path, bool shouldIgnore)
        {
            // Arrange
            var method = typeof(WatchCommand).GetMethod("ShouldIgnorePath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            // Act
            var result = (bool)method!.Invoke(null, [path])!;

            // Assert
            Assert.Equal(shouldIgnore, result);
        }

        [Fact]
        public void Should_ignore_generated_files()
        {
            // Arrange
            var method = typeof(WatchCommand).GetMethod("ShouldIgnorePath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            // Act & Assert
            Assert.True((bool)method!.Invoke(null, ["/project/src/OrderAggregate.generated.cs"])!);
        }

        [Fact]
        public void Should_not_ignore_normal_source_files()
        {
            // Arrange
            var method = typeof(WatchCommand).GetMethod("ShouldIgnorePath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            // Act
            var result = (bool)method!.Invoke(null, ["/project/src/OrderAggregate.cs"])!;

            // Assert
            Assert.False(result);
        }
    }

    public class Constructor : WatchCommandTests
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange & Act
            var sut = new WatchCommand();

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_initialize_change_detector()
        {
            // Arrange & Act
            var sut = new WatchCommand();

            // Assert - Uses reflection to check private field
            var field = typeof(WatchCommand).GetField("_changeDetector", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            var changeDetector = field!.GetValue(sut);
            Assert.NotNull(changeDetector);
        }
    }

    public class LoadConfigAsyncMethod : WatchCommandTests
    {
        [Fact]
        public async Task Should_return_default_config_when_no_config_file()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var method = typeof(WatchCommand).GetMethod("LoadConfigAsync", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var task = (Task<ErikLieben.FA.ES.CLI.Configuration.Config>)method!.Invoke(null, [tempDir, CancellationToken.None])!;
                var result = await task;

                // Assert
                Assert.NotNull(result);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_load_config_from_elfa_directory()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var elfaDir = Path.Combine(tempDir, ".elfa");
            Directory.CreateDirectory(elfaDir);
            var configContent = """
                {
                    "AdditionalJsonSerializables": ["MyType"],
                    "ES": {
                        "EnableDiagnostics": true
                    }
                }
                """;
            File.WriteAllText(Path.Combine(elfaDir, "config.json"), configContent);

            try
            {
                var method = typeof(WatchCommand).GetMethod("LoadConfigAsync", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var task = (Task<ErikLieben.FA.ES.CLI.Configuration.Config>)method!.Invoke(null, [tempDir, CancellationToken.None])!;
                var result = await task;

                // Assert
                Assert.NotNull(result);
                Assert.Single(result.AdditionalJsonSerializables);
                Assert.Equal("MyType", result.AdditionalJsonSerializables[0]);
                Assert.True(result.Es.EnableDiagnostics);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
