using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Logging;
using Spectre.Console;
using Spectre.Console.Testing;

namespace ErikLieben.FA.ES.CLI.Tests.Logging;

public class ConsoleActivityLoggerTests
{
    public class Constructor : ConsoleActivityLoggerTests
    {
        [Fact]
        public void Should_create_instance_with_console()
        {
            // Arrange
            var console = new TestConsole();

            // Act
            var sut = new ConsoleActivityLogger(console);

            // Assert
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_verbose_flag()
        {
            // Arrange
            var console = new TestConsole();

            // Act
            var sut = new ConsoleActivityLogger(console, verbose: true);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class LogMethod : ConsoleActivityLoggerTests
    {
        [Fact]
        public void Should_record_entry()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.Log(ActivityType.Info, "Test message", "Aggregate", "TestAgg");

            // Assert
            var entries = sut.GetActivityLog();
            Assert.Single(entries);
            Assert.Equal(ActivityType.Info, entries[0].Type);
            Assert.Equal("Test message", entries[0].Message);
            Assert.Equal("Aggregate", entries[0].EntityType);
            Assert.Equal("TestAgg", entries[0].EntityName);
        }

        [Fact]
        public void Should_raise_OnActivity_event()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);
            ActivityLogEntry? captured = null;
            sut.OnActivity += entry => captured = entry;

            // Act
            sut.Log(ActivityType.FileGenerated, "Generated file");

            // Assert
            Assert.NotNull(captured);
            Assert.Equal(ActivityType.FileGenerated, captured.Type);
        }

        [Fact]
        public void Should_output_error_to_console()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.Log(ActivityType.Error, "Error message");

            // Assert
            Assert.Contains("Error message", console.Output);
        }

        [Fact]
        public void Should_output_warning_to_console()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.Log(ActivityType.Warning, "Warning message");

            // Assert
            Assert.Contains("Warning message", console.Output);
        }

        [Fact]
        public void Should_output_analysis_started_to_console()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.Log(ActivityType.AnalysisStarted, "Starting");

            // Assert
            Assert.Contains("Starting", console.Output);
        }

        [Fact]
        public void Should_output_analysis_completed_to_console()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.Log(ActivityType.AnalysisCompleted, "Done");

            // Assert
            Assert.Contains("Done", console.Output);
        }

        [Fact]
        public void Should_output_generation_started_to_console()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.Log(ActivityType.GenerationStarted, "Generating");

            // Assert
            Assert.Contains("Generating", console.Output);
        }

        [Fact]
        public void Should_output_generation_completed_to_console()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.Log(ActivityType.GenerationCompleted, "Complete");

            // Assert
            Assert.Contains("Complete", console.Output);
        }

        [Fact]
        public void Should_not_output_info_when_not_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: false);

            // Act
            sut.Log(ActivityType.Info, "Info message");

            // Assert
            Assert.DoesNotContain("Info message", console.Output);
        }

        [Fact]
        public void Should_output_info_when_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: true);

            // Act
            sut.Log(ActivityType.Info, "Info message");

            // Assert
            Assert.Contains("Info message", console.Output);
        }

        [Fact]
        public void Should_not_output_file_generated_when_not_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: false);

            // Act
            sut.Log(ActivityType.FileGenerated, "File generated");

            // Assert
            Assert.DoesNotContain("File generated", console.Output);
        }

        [Fact]
        public void Should_output_file_generated_when_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: true);

            // Act
            sut.Log(ActivityType.FileGenerated, "File generated");

            // Assert
            Assert.Contains("File generated", console.Output);
        }

        [Fact]
        public void Should_not_output_file_skipped_when_not_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: false);

            // Act
            sut.Log(ActivityType.FileSkipped, "File skipped");

            // Assert
            Assert.DoesNotContain("File skipped", console.Output);
        }

        [Fact]
        public void Should_output_file_skipped_when_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: true);

            // Act
            sut.Log(ActivityType.FileSkipped, "File skipped");

            // Assert
            Assert.Contains("File skipped", console.Output);
        }

        [Fact]
        public void Should_include_entity_info_in_output()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.Log(ActivityType.AnalysisStarted, "Analyzing", "Aggregate", "OrderAggregate");

            // Assert
            Assert.Contains("Aggregate", console.Output);
            Assert.Contains("OrderAggregate", console.Output);
        }
    }

    public class LogErrorMethod : ConsoleActivityLoggerTests
    {
        [Fact]
        public void Should_record_error_entry()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.LogError("Error message");

            // Assert
            var entries = sut.GetActivityLog();
            Assert.Single(entries);
            Assert.Equal(ActivityType.Error, entries[0].Type);
        }

        [Fact]
        public void Should_record_exception()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);
            var exception = new InvalidOperationException("Test exception");

            // Act
            sut.LogError("Error message", exception);

            // Assert
            var entries = sut.GetActivityLog();
            Assert.Same(exception, entries[0].Exception);
        }

        [Fact]
        public void Should_output_error_to_console()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.LogError("Error message");

            // Assert
            Assert.Contains("Error", console.Output);
            Assert.Contains("Error message", console.Output);
        }

        [Fact]
        public void Should_output_exception_when_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: true);
            var exception = new InvalidOperationException("Test exception details");

            // Act
            sut.LogError("Error message", exception);

            // Assert
            Assert.Contains("Test exception details", console.Output);
        }

        [Fact]
        public void Should_not_output_exception_when_not_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: false);
            var exception = new InvalidOperationException("Test exception details");

            // Act
            sut.LogError("Error message", exception);

            // Assert
            Assert.DoesNotContain("Test exception details", console.Output);
        }

        [Fact]
        public void Should_raise_OnActivity_event()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);
            ActivityLogEntry? captured = null;
            sut.OnActivity += entry => captured = entry;

            // Act
            sut.LogError("Error");

            // Assert
            Assert.NotNull(captured);
            Assert.Equal(ActivityType.Error, captured.Type);
        }
    }

    public class LogProgressMethod : ConsoleActivityLoggerTests
    {
        [Fact]
        public void Should_record_progress_entry()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            sut.LogProgress(5, 10, "Processing");

            // Assert
            var entries = sut.GetActivityLog();
            Assert.Single(entries);
            Assert.Equal(ActivityType.Progress, entries[0].Type);
        }

        [Fact]
        public void Should_not_output_progress_when_not_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: false);

            // Act
            sut.LogProgress(5, 10, "Processing");

            // Assert
            Assert.DoesNotContain("Processing", console.Output);
        }

        [Fact]
        public void Should_output_progress_when_verbose()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: true);

            // Act
            sut.LogProgress(5, 10, "Processing");

            // Assert
            Assert.Contains("50%", console.Output);
            Assert.Contains("Processing", console.Output);
        }

        [Fact]
        public void Should_handle_zero_total()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console, verbose: true);

            // Act
            sut.LogProgress(5, 0, "Processing");

            // Assert
            Assert.Contains("0%", console.Output);
        }

        [Fact]
        public void Should_raise_OnActivity_event()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);
            ActivityLogEntry? captured = null;
            sut.OnActivity += entry => captured = entry;

            // Act
            sut.LogProgress(5, 10, "Processing");

            // Assert
            Assert.NotNull(captured);
            Assert.Equal(ActivityType.Progress, captured.Type);
        }
    }

    public class GetActivityLogMethod : ConsoleActivityLoggerTests
    {
        [Fact]
        public async Task Should_return_entries_in_order()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);
            sut.Log(ActivityType.Info, "First");
            await Task.Delay(10);
            sut.Log(ActivityType.Info, "Second");

            // Act
            var entries = sut.GetActivityLog();

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.Equal("First", entries[0].Message);
            Assert.Equal("Second", entries[1].Message);
        }

        [Fact]
        public void Should_return_empty_for_fresh_logger()
        {
            // Arrange
            var console = new TestConsole();
            var sut = new ConsoleActivityLogger(console);

            // Act
            var entries = sut.GetActivityLog();

            // Assert
            Assert.Empty(entries);
        }
    }
}
