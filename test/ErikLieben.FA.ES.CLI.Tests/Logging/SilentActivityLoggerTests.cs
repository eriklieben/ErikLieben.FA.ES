#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Logging;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Logging;

public class SilentActivityLoggerTests
{
    [Fact]
    public void Log_ShouldRecordEntry()
    {
        // Arrange
        var logger = new SilentActivityLogger();

        // Act
        logger.Log(ActivityType.Info, "Test message", "Aggregate", "OrderAggregate");

        // Assert
        var entries = logger.GetActivityLog();
        Assert.Single(entries);
        Assert.Equal(ActivityType.Info, entries[0].Type);
        Assert.Equal("Test message", entries[0].Message);
        Assert.Equal("Aggregate", entries[0].EntityType);
        Assert.Equal("OrderAggregate", entries[0].EntityName);
    }

    [Fact]
    public void Log_ShouldRaiseOnActivityEvent()
    {
        // Arrange
        var logger = new SilentActivityLogger();
        ActivityLogEntry? captured = null;
        logger.OnActivity += entry => captured = entry;

        // Act
        logger.Log(ActivityType.FileGenerated, "Generated file");

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(ActivityType.FileGenerated, captured.Type);
        Assert.Equal("Generated file", captured.Message);
    }

    [Fact]
    public void LogError_ShouldRecordErrorWithException()
    {
        // Arrange
        var logger = new SilentActivityLogger();
        var exception = new InvalidOperationException("Test exception");

        // Act
        logger.LogError("An error occurred", exception);

        // Assert
        var entries = logger.GetActivityLog();
        Assert.Single(entries);
        Assert.Equal(ActivityType.Error, entries[0].Type);
        Assert.Equal("An error occurred", entries[0].Message);
        Assert.Same(exception, entries[0].Exception);
    }

    [Fact]
    public void HasErrors_ShouldReturnTrueWhenErrorsExist()
    {
        // Arrange
        var logger = new SilentActivityLogger();

        // Act
        logger.Log(ActivityType.Info, "Info message");
        logger.LogError("Error message");

        // Assert
        Assert.True(logger.HasErrors());
    }

    [Fact]
    public void HasErrors_ShouldReturnFalseWhenNoErrors()
    {
        // Arrange
        var logger = new SilentActivityLogger();

        // Act
        logger.Log(ActivityType.Info, "Info message");
        logger.Log(ActivityType.Warning, "Warning message");

        // Assert
        Assert.False(logger.HasErrors());
    }

    [Fact]
    public void CountByType_ShouldReturnCorrectCount()
    {
        // Arrange
        var logger = new SilentActivityLogger();

        // Act
        logger.Log(ActivityType.FileGenerated, "File 1");
        logger.Log(ActivityType.FileGenerated, "File 2");
        logger.Log(ActivityType.FileGenerated, "File 3");
        logger.Log(ActivityType.Info, "Info");

        // Assert
        Assert.Equal(3, logger.CountByType(ActivityType.FileGenerated));
        Assert.Equal(1, logger.CountByType(ActivityType.Info));
        Assert.Equal(0, logger.CountByType(ActivityType.Error));
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        var logger = new SilentActivityLogger();
        logger.Log(ActivityType.Info, "Message 1");
        logger.Log(ActivityType.Info, "Message 2");

        // Act
        logger.Clear();

        // Assert
        Assert.Empty(logger.GetActivityLog());
    }

    [Fact]
    public void LogProgress_ShouldRecordProgressEntry()
    {
        // Arrange
        var logger = new SilentActivityLogger();

        // Act
        logger.LogProgress(5, 10, "Processing...");

        // Assert
        var entries = logger.GetActivityLog();
        Assert.Single(entries);
        Assert.Equal(ActivityType.Progress, entries[0].Type);
        Assert.Equal("Processing...", entries[0].Message);
    }
}
