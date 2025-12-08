using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Generation;
using ErikLieben.FA.ES.CLI.IO;
using ErikLieben.FA.ES.CLI.Logging;
using ErikLieben.FA.ES.CLI.Model;
using NSubstitute;

namespace ErikLieben.FA.ES.CLI.Tests.Generation;

public class VersionTokenCodeGeneratorTests
{
    public class Constructor : VersionTokenCodeGeneratorTests
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            var config = new Config();

            // Act
            var sut = new VersionTokenCodeGenerator(logger, codeWriter, config);

            // Assert
            Assert.NotNull(sut);
            Assert.Equal("Version Tokens", sut.Name);
        }
    }

    public class GenerateAsyncMethod : VersionTokenCodeGeneratorTests
    {
        [Fact]
        public async Task Should_log_generation_started()
        {
            // Arrange
            var logger = new SilentActivityLogger();
            var codeWriter = new InMemoryCodeWriter();
            var config = new Config();
            var sut = new VersionTokenCodeGenerator(logger, codeWriter, config);
            var solution = new SolutionDefinition
            {
                SolutionName = "Test",
                Generator = new GeneratorInformation { Version = "1.0.0" }
            };

            // Act
            await sut.GenerateAsync(solution, Path.GetTempPath());

            // Assert
            Assert.Contains(logger.GetActivityLog(), e =>
                e.Type == ActivityType.GenerationStarted &&
                e.Message.Contains("Version Tokens"));
        }

        [Fact]
        public async Task Should_log_generation_completed()
        {
            // Arrange
            var logger = new SilentActivityLogger();
            var codeWriter = new InMemoryCodeWriter();
            var config = new Config();
            var sut = new VersionTokenCodeGenerator(logger, codeWriter, config);
            var solution = new SolutionDefinition
            {
                SolutionName = "Test",
                Generator = new GeneratorInformation { Version = "1.0.0" }
            };

            // Act
            await sut.GenerateAsync(solution, Path.GetTempPath());

            // Assert
            Assert.Contains(logger.GetActivityLog(), e =>
                e.Type == ActivityType.GenerationCompleted &&
                e.Message.Contains("Version Tokens"));
        }
    }
}
