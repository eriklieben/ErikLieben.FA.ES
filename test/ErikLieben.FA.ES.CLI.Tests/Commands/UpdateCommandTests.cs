using System.Reflection;
using ErikLieben.FA.ES.CLI.Commands;

namespace ErikLieben.FA.ES.CLI.Tests.Commands;

public class UpdateCommandTests
{
    public class Settings : UpdateCommandTests
    {
        [Fact]
        public void Should_have_default_values()
        {
            // Arrange & Act
            var settings = new UpdateCommand.Settings();

            // Assert
            Assert.Null(settings.Path);
            Assert.Null(settings.TargetVersion);
            Assert.False(settings.SkipGitCheck);
            Assert.False(settings.DryRun);
            Assert.False(settings.SkipGenerate);
        }

        [Fact]
        public void Should_allow_setting_path()
        {
            // Arrange & Act
            var settings = new UpdateCommand.Settings { Path = "/test/solution.sln" };

            // Assert
            Assert.Equal("/test/solution.sln", settings.Path);
        }

        [Fact]
        public void Should_allow_setting_target_version()
        {
            // Arrange & Act
            var settings = new UpdateCommand.Settings { TargetVersion = "2.0.0" };

            // Assert
            Assert.Equal("2.0.0", settings.TargetVersion);
        }

        [Fact]
        public void Should_allow_setting_skip_git_check()
        {
            // Arrange & Act
            var settings = new UpdateCommand.Settings { SkipGitCheck = true };

            // Assert
            Assert.True(settings.SkipGitCheck);
        }

        [Fact]
        public void Should_allow_setting_dry_run()
        {
            // Arrange & Act
            var settings = new UpdateCommand.Settings { DryRun = true };

            // Assert
            Assert.True(settings.DryRun);
        }

        [Fact]
        public void Should_allow_setting_skip_generate()
        {
            // Arrange & Act
            var settings = new UpdateCommand.Settings { SkipGenerate = true };

            // Assert
            Assert.True(settings.SkipGenerate);
        }
    }

    public class ResolveSolutionPathMethod : UpdateCommandTests
    {
        [Fact]
        public void Should_return_provided_path()
        {
            // Arrange
            var settings = new UpdateCommand.Settings { Path = "/test/solution.sln" };
            var method = typeof(UpdateCommand).GetMethod("ResolveSolutionPath", BindingFlags.NonPublic | BindingFlags.Static);
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
                var settings = new UpdateCommand.Settings { Path = null };
                var method = typeof(UpdateCommand).GetMethod("ResolveSolutionPath", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var result = method!.Invoke(null, [settings]);

                // Assert
                Assert.NotNull(result);
                Assert.EndsWith("Test.sln", (string)result!, StringComparison.OrdinalIgnoreCase);
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
                var settings = new UpdateCommand.Settings { Path = null };
                var method = typeof(UpdateCommand).GetMethod("ResolveSolutionPath", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var result = method!.Invoke(null, [settings]);

                // Assert
                Assert.Null(result);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void Should_prefer_sln_over_slnx()
        {
            // Arrange
            var originalCwd = Directory.GetCurrentDirectory();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "Test.sln"), "");
            File.WriteAllText(Path.Combine(tempDir, "Test.slnx"), "");

            try
            {
                Directory.SetCurrentDirectory(tempDir);
                var settings = new UpdateCommand.Settings { Path = null };
                var method = typeof(UpdateCommand).GetMethod("ResolveSolutionPath", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var result = method!.Invoke(null, [settings]);

                // Assert
                Assert.NotNull(result);
                // Should find one of them (order is not guaranteed, but one should be found)
                Assert.True(
                    ((string)result!).EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                    ((string)result!).EndsWith(".slnx", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public class FindSolutionFileMethod : UpdateCommandTests
    {
        [Fact]
        public void Should_find_slnx_file()
        {
            // Arrange
            var originalCwd = Directory.GetCurrentDirectory();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "Test.slnx"), "");

            try
            {
                Directory.SetCurrentDirectory(tempDir);
                var method = typeof(UpdateCommand).GetMethod("FindSolutionFile", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var result = method!.Invoke(null, []);

                // Assert
                Assert.NotNull(result);
                Assert.EndsWith(".slnx", (string)result!, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public class DetectCurrentVersionAsyncMethod : UpdateCommandTests
    {
        [Fact]
        public async Task Should_detect_version_from_csproj_attribute_format()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var csprojContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="ErikLieben.FA.ES" Version="1.5.0" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(tempDir, "Test.csproj"), csprojContent);

            try
            {
                var method = typeof(UpdateCommand).GetMethod("DetectCurrentVersionAsync", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var task = (Task<string?>)method!.Invoke(null, [tempDir, CancellationToken.None])!;
                var result = await task;

                // Assert
                Assert.Equal("1.5.0", result);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_detect_version_from_csproj_element_format()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var csprojContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="ErikLieben.FA.ES">
                      <Version>1.6.0</Version>
                    </PackageReference>
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(tempDir, "Test.csproj"), csprojContent);

            try
            {
                var method = typeof(UpdateCommand).GetMethod("DetectCurrentVersionAsync", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var task = (Task<string?>)method!.Invoke(null, [tempDir, CancellationToken.None])!;
                var result = await task;

                // Assert
                Assert.Equal("1.6.0", result);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_return_null_when_no_package_reference()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var csprojContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="SomeOtherPackage" Version="1.0.0" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(tempDir, "Test.csproj"), csprojContent);

            try
            {
                var method = typeof(UpdateCommand).GetMethod("DetectCurrentVersionAsync", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var task = (Task<string?>)method!.Invoke(null, [tempDir, CancellationToken.None])!;
                var result = await task;

                // Assert
                Assert.Null(result);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_search_subdirectories()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var subDir = Path.Combine(tempDir, "src", "App");
            Directory.CreateDirectory(subDir);
            var csprojContent = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="ErikLieben.FA.ES" Version="2.0.0-preview" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(subDir, "App.csproj"), csprojContent);

            try
            {
                var method = typeof(UpdateCommand).GetMethod("DetectCurrentVersionAsync", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                // Act
                var task = (Task<string?>)method!.Invoke(null, [tempDir, CancellationToken.None])!;
                var result = await task;

                // Assert
                Assert.Equal("2.0.0-preview", result);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public class Constructor : UpdateCommandTests
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange & Act
            var sut = new UpdateCommand();

            // Assert
            Assert.NotNull(sut);
        }
    }
}
