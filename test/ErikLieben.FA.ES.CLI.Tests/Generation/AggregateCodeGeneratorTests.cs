using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Generation;
using ErikLieben.FA.ES.CLI.IO;
using ErikLieben.FA.ES.CLI.Logging;
using ErikLieben.FA.ES.CLI.Model;
using NSubstitute;

namespace ErikLieben.FA.ES.CLI.Tests.Generation;

public class AggregateCodeGeneratorTests
{
    public class Constructor : AggregateCodeGeneratorTests
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange
            var logger = Substitute.For<IActivityLogger>();
            var codeWriter = Substitute.For<ICodeWriter>();
            var config = new Config();

            // Act
            var sut = new AggregateCodeGenerator(logger, codeWriter, config);

            // Assert
            Assert.NotNull(sut);
            Assert.Equal("Aggregates", sut.Name);
        }
    }

    public class GenerateAsyncMethod : AggregateCodeGeneratorTests
    {
        [Fact]
        public async Task Should_log_generation_started()
        {
            // Arrange
            var logger = new SilentActivityLogger();
            var codeWriter = new InMemoryCodeWriter();
            var config = new Config();
            var sut = new AggregateCodeGenerator(logger, codeWriter, config);
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
                e.Message.Contains("Aggregates"));
        }

        [Fact]
        public async Task Should_log_generation_completed()
        {
            // Arrange
            var logger = new SilentActivityLogger();
            var codeWriter = new InMemoryCodeWriter();
            var config = new Config();
            var sut = new AggregateCodeGenerator(logger, codeWriter, config);
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
                e.Message.Contains("Aggregates"));
        }

        [Fact]
        public async Task Should_skip_ErikLieben_FA_ES_projects()
        {
            // Arrange
            var logger = new SilentActivityLogger();
            var codeWriter = new InMemoryCodeWriter();
            var config = new Config();
            var sut = new AggregateCodeGenerator(logger, codeWriter, config);
            var solution = new SolutionDefinition
            {
                SolutionName = "Test",
                Generator = new GeneratorInformation { Version = "1.0.0" },
                Projects =
                [
                    new ProjectDefinition
                    {
                        Name = "ErikLieben.FA.ES",
                        Namespace = "ErikLieben.FA.ES",
                        FileLocation = "ErikLieben.FA.ES.csproj",
                        Aggregates =
                        [
                            new AggregateDefinition
                            {
                                IdentifierName = "TestAggregate",
                                ObjectName = "TestAggregate",
                                IdentifierType = "TestAggregateId",
                                IdentifierTypeNamespace = "Test",
                                Namespace = "Test",
                                IsPartialClass = true,
                                FileLocations = ["Test.cs"]
                            }
                        ]
                    }
                ]
            };

            // Act
            await sut.GenerateAsync(solution, Path.GetTempPath());

            // Assert - Should not generate for ErikLieben.FA.ES projects
            Assert.Empty(codeWriter.GetWrittenFiles());
        }

        [Fact]
        public async Task Should_warn_for_non_partial_classes()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "OrderAggregate.cs");
            File.WriteAllText(sourceFile, "public class OrderAggregate { }");

            try
            {
                var logger = new SilentActivityLogger();
                var codeWriter = new InMemoryCodeWriter();
                var config = new Config();
                var sut = new AggregateCodeGenerator(logger, codeWriter, config);
                var solution = new SolutionDefinition
                {
                    SolutionName = "Test",
                    Generator = new GeneratorInformation { Version = "1.0.0" },
                    Projects =
                    [
                        new ProjectDefinition
                        {
                            Name = "Demo.App",
                            Namespace = "Demo.App",
                            FileLocation = "Demo.App.csproj",
                            Aggregates =
                            [
                                new AggregateDefinition
                                {
                                    IdentifierName = "OrderAggregate",
                                    ObjectName = "Order",
                                    IdentifierType = "OrderId",
                                    IdentifierTypeNamespace = "Demo.App",
                                    Namespace = "Demo.App",
                                    IsPartialClass = false, // Not partial
                                    FileLocations = ["OrderAggregate.cs"]
                                }
                            ]
                        }
                    ]
                };

                // Act
                await sut.GenerateAsync(solution, tempDir);

                // Assert
                Assert.Contains(logger.GetActivityLog(), e =>
                    e.Type == ActivityType.Warning &&
                    e.Message.Contains("partial"));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_skip_generated_files()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "OrderAggregate.generated.cs");
            File.WriteAllText(sourceFile, "public partial class OrderAggregate { }");

            try
            {
                var logger = new SilentActivityLogger();
                var codeWriter = new InMemoryCodeWriter();
                var config = new Config();
                var sut = new AggregateCodeGenerator(logger, codeWriter, config);
                var solution = new SolutionDefinition
                {
                    SolutionName = "Test",
                    Generator = new GeneratorInformation { Version = "1.0.0" },
                    Projects =
                    [
                        new ProjectDefinition
                        {
                            Name = "Demo.App",
                            Namespace = "Demo.App",
                            FileLocation = "Demo.App.csproj",
                            Aggregates =
                            [
                                new AggregateDefinition
                                {
                                    IdentifierName = "OrderAggregate",
                                    ObjectName = "Order",
                                    IdentifierType = "OrderId",
                                    IdentifierTypeNamespace = "Demo.App",
                                    Namespace = "Demo.App",
                                    IsPartialClass = true,
                                    FileLocations = ["OrderAggregate.generated.cs"]
                                }
                            ]
                        }
                    ]
                };

                // Act
                await sut.GenerateAsync(solution, tempDir);

                // Assert - Should not generate for .generated files
                Assert.Empty(codeWriter.GetWrittenFiles());
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public async Task Should_log_info_for_valid_partial_class()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "OrderAggregate.cs");
            File.WriteAllText(sourceFile, "public partial class OrderAggregate { }");

            try
            {
                var logger = new SilentActivityLogger();
                var codeWriter = new InMemoryCodeWriter();
                var config = new Config();
                var sut = new AggregateCodeGenerator(logger, codeWriter, config);
                var solution = new SolutionDefinition
                {
                    SolutionName = "Test",
                    Generator = new GeneratorInformation { Version = "1.0.0" },
                    Projects =
                    [
                        new ProjectDefinition
                        {
                            Name = "Demo.App",
                            Namespace = "Demo.App",
                            FileLocation = "Demo.App.csproj",
                            Aggregates =
                            [
                                new AggregateDefinition
                                {
                                    IdentifierName = "OrderAggregate",
                                    ObjectName = "Order",
                                    IdentifierType = "OrderId",
                                    IdentifierTypeNamespace = "Demo.App",
                                    Namespace = "Demo.App",
                                    IsPartialClass = true,
                                    FileLocations = ["OrderAggregate.cs"],
                                    Events = [],
                                    Commands = [],
                                    Properties = [],
                                    Constructors =
                                    [
                                        new ConstructorDefinition
                                        {
                                            Parameters = []
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                };

                // Act
                await sut.GenerateAsync(solution, tempDir);

                // Assert - Should log that it's generating for this aggregate
                Assert.Contains(logger.GetActivityLog(), e =>
                    e.Type == ActivityType.Info &&
                    e.Message.Contains("OrderAggregate"));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
