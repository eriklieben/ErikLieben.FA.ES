using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Commands;
using ErikLieben.FA.ES.CLI.Logging;
using NSubstitute;
using ActivityType = ErikLieben.FA.ES.CLI.Abstractions.ActivityType;

namespace ErikLieben.FA.ES.CLI.Tests.Logging;

public class WatchDisplayActivityLoggerTests
{
    public class Constructor : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_create_instance_with_display()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();

            // Act
            var sut = new WatchDisplayActivityLogger(display);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class LogMethod : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_log_activity_to_display()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            sut.Log(ActivityType.Info, "Test message");

            // Assert
            display.Received(1).LogActivity(WatchDisplay.ActivityType.Info, "Test message");
        }

        [Fact]
        public void Should_add_entry_to_activity_log()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            sut.Log(ActivityType.Info, "Test message", "TestEntity", "TestName");

            // Assert
            var log = sut.GetActivityLog();
            Assert.Single(log);
            Assert.Equal(ActivityType.Info, log[0].Type);
            Assert.Equal("Test message", log[0].Message);
            Assert.Equal("TestEntity", log[0].EntityType);
            Assert.Equal("TestName", log[0].EntityName);
        }

        [Fact]
        public void Should_raise_OnActivity_event()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            ActivityLogEntry? receivedEntry = null;
            sut.OnActivity += entry => receivedEntry = entry;

            // Act
            sut.Log(ActivityType.Warning, "Warning message");

            // Assert
            Assert.NotNull(receivedEntry);
            Assert.Equal(ActivityType.Warning, receivedEntry!.Type);
            Assert.Equal("Warning message", receivedEntry.Message);
        }

        [Theory]
        [InlineData(ActivityType.Info, WatchDisplay.ActivityType.Info)]
        [InlineData(ActivityType.Warning, WatchDisplay.ActivityType.Warning)]
        [InlineData(ActivityType.Error, WatchDisplay.ActivityType.Error)]
        [InlineData(ActivityType.FileGenerated, WatchDisplay.ActivityType.Info)]
        [InlineData(ActivityType.FileSkipped, WatchDisplay.ActivityType.Info)]
        [InlineData(ActivityType.AnalysisStarted, WatchDisplay.ActivityType.Info)]
        [InlineData(ActivityType.AnalysisCompleted, WatchDisplay.ActivityType.RegenCompleted)]
        [InlineData(ActivityType.GenerationStarted, WatchDisplay.ActivityType.RegenStarted)]
        [InlineData(ActivityType.GenerationCompleted, WatchDisplay.ActivityType.RegenCompleted)]
        [InlineData(ActivityType.Progress, WatchDisplay.ActivityType.Info)]
        public void Should_map_activity_type_to_display_activity_type(ActivityType inputType, WatchDisplay.ActivityType expectedType)
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            sut.Log(inputType, "Test");

            // Assert
            display.Received(1).LogActivity(expectedType, "Test");
        }
    }

    public class LogErrorMethod : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_log_error_to_display()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            sut.LogError("Error occurred");

            // Assert
            display.Received(1).LogActivity(WatchDisplay.ActivityType.Error, "Error occurred");
        }

        [Fact]
        public void Should_include_exception_message_when_provided()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            var exception = new InvalidOperationException("Something went wrong");

            // Act
            sut.LogError("Error occurred", exception);

            // Assert
            display.Received(1).LogActivity(
                WatchDisplay.ActivityType.Error,
                "Error occurred: Something went wrong");
        }

        [Fact]
        public void Should_add_error_entry_to_activity_log()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            var exception = new ArgumentException("Bad argument");

            // Act
            sut.LogError("Failed to process", exception);

            // Assert
            var log = sut.GetActivityLog();
            Assert.Single(log);
            Assert.Equal(ActivityType.Error, log[0].Type);
            Assert.Equal("Failed to process", log[0].Message);
            Assert.Equal(exception, log[0].Exception);
        }

        [Fact]
        public void Should_raise_OnActivity_event_for_error()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            ActivityLogEntry? receivedEntry = null;
            sut.OnActivity += entry => receivedEntry = entry;

            // Act
            sut.LogError("Critical failure");

            // Assert
            Assert.NotNull(receivedEntry);
            Assert.Equal(ActivityType.Error, receivedEntry!.Type);
        }
    }

    public class LogProgressMethod : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_set_analysis_progress_on_display()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            sut.LogProgress(50, 100, "Processing files...");

            // Assert
            display.Received(1).SetAnalysisProgress(50, 100, "Processing files...");
        }

        [Fact]
        public void Should_add_progress_entry_to_activity_log()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            sut.LogProgress(25, 100, "Analyzing...");

            // Assert
            var log = sut.GetActivityLog();
            Assert.Single(log);
            Assert.Equal(ActivityType.Progress, log[0].Type);
            Assert.Equal("Analyzing...", log[0].Message);
        }

        [Fact]
        public void Should_raise_OnActivity_event_for_progress()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            ActivityLogEntry? receivedEntry = null;
            sut.OnActivity += entry => receivedEntry = entry;

            // Act
            sut.LogProgress(75, 100, "Almost done");

            // Assert
            Assert.NotNull(receivedEntry);
            Assert.Equal(ActivityType.Progress, receivedEntry!.Type);
            Assert.Equal("Almost done", receivedEntry.Message);
        }
    }

    public class GetActivityLogMethod : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_return_empty_list_when_no_activity()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            var log = sut.GetActivityLog();

            // Assert
            Assert.Empty(log);
        }

        [Fact]
        public void Should_return_entries_ordered_by_timestamp()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            sut.Log(ActivityType.Info, "First");
            sut.Log(ActivityType.Warning, "Second");
            sut.Log(ActivityType.Error, "Third");

            // Act
            var log = sut.GetActivityLog();

            // Assert
            Assert.Equal(3, log.Count);
            Assert.True(log[0].Timestamp <= log[1].Timestamp);
            Assert.True(log[1].Timestamp <= log[2].Timestamp);
        }

        [Fact]
        public void Should_return_read_only_list()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            sut.Log(ActivityType.Info, "Test");

            // Act
            var result = sut.GetActivityLog();

            // Assert
            Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<ActivityLogEntry>>(result);
        }
    }

    public class LogFileGeneratedMethod : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_log_file_generated_activity()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            var filePath = Path.Combine("Project", "src", "MyClass.cs");

            // Act
            sut.LogFileGenerated(filePath);

            // Assert
            display.Received(1).LogActivity(
                WatchDisplay.ActivityType.Info,
                "Generated: MyClass.cs");
        }

        [Fact]
        public void Should_extract_file_name_from_path()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            var filePath = Path.Combine("Some", "Deep", "Path", "Generated", "MyAggregate.Generated.cs");

            // Act
            sut.LogFileGenerated(filePath);

            // Assert
            var log = sut.GetActivityLog();
            Assert.Contains("MyAggregate.Generated.cs", log[0].Message);
        }
    }

    public class LogFileSkippedMethod : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_log_file_skipped_activity()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            var filePath = Path.Combine("Project", "src", "Unchanged.cs");

            // Act
            sut.LogFileSkipped(filePath);

            // Assert
            display.Received(1).LogActivity(
                WatchDisplay.ActivityType.Info,
                "Skipped (unchanged): Unchanged.cs");
        }
    }

    public class SetEntityCountsMethod : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_forward_counts_to_display()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            sut.SetEntityCounts(10, 5, 3, 25);

            // Assert
            display.Received(1).SetEntityCounts(10, 5, 3, 25);
        }

        [Fact]
        public void Should_handle_zero_counts()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            sut.SetEntityCounts(0, 0, 0, 0);

            // Assert
            display.Received(1).SetEntityCounts(0, 0, 0, 0);
        }
    }

    public class OnActivityEventTests : WatchDisplayActivityLoggerTests
    {
        [Fact]
        public void Should_support_multiple_event_handlers()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);
            var handler1Called = false;
            var handler2Called = false;
            sut.OnActivity += _ => handler1Called = true;
            sut.OnActivity += _ => handler2Called = true;

            // Act
            sut.Log(ActivityType.Info, "Test");

            // Assert
            Assert.True(handler1Called);
            Assert.True(handler2Called);
        }

        [Fact]
        public void Should_work_without_event_handlers()
        {
            // Arrange
            var display = Substitute.For<IWatchDisplay>();
            var sut = new WatchDisplayActivityLogger(display);

            // Act
            var exception = Record.Exception(() =>
            {
                sut.Log(ActivityType.Info, "Test");
                sut.LogError("Error");
                sut.LogProgress(1, 10, "Progress");
            });

            // Assert
            Assert.Null(exception);
        }
    }
}
