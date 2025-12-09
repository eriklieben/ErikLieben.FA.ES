using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Generation;
using ErikLieben.FA.ES.CLI.IO;
using ErikLieben.FA.ES.CLI.Logging;
using ErikLieben.FA.ES.CLI.Model;
using NSubstitute;

namespace ErikLieben.FA.ES.CLI.Tests.Generation;

public class ExtensionCodeGeneratorTests
{
    public class Constructor : ExtensionCodeGeneratorTests
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            var config = new Config();

            // Act
            var sut = new ExtensionCodeGenerator(logger, codeWriter, config);

            // Assert
            Assert.NotNull(sut);
            Assert.Equal("Extensions", sut.Name);
        }
    }

    public class GenerateAsyncMethod : ExtensionCodeGeneratorTests
    {
        [Fact]
        public async Task Should_log_generation_started()
        {
            // Arrange
            var logger = new SilentActivityLogger();
            var codeWriter = new InMemoryCodeWriter();
            var config = new Config();
            var sut = new ExtensionCodeGenerator(logger, codeWriter, config);
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
                e.Message.Contains("Extensions"));
        }

        [Fact]
        public async Task Should_log_generation_completed()
        {
            // Arrange
            var logger = new SilentActivityLogger();
            var codeWriter = new InMemoryCodeWriter();
            var config = new Config();
            var sut = new ExtensionCodeGenerator(logger, codeWriter, config);
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
                e.Message.Contains("Extensions"));
        }
    }
}
