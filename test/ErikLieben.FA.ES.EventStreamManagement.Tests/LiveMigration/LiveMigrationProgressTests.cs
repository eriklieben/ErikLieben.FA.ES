using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.LiveMigration;

public class LiveMigrationProgressTests
{
    [Fact]
    public void Should_calculate_events_behind_correctly()
    {
        // Arrange
        var sut = new LiveMigrationProgress
        {
            Iteration = 1,
            SourceVersion = 100,
            TargetVersion = 95,
            EventsCopiedThisIteration = 5,
            TotalEventsCopied = 5,
            ElapsedTime = TimeSpan.FromSeconds(1)
        };

        // Assert
        Assert.Equal(5, sut.EventsBehind);
    }

    [Fact]
    public void Should_return_zero_events_behind_when_synced()
    {
        // Arrange
        var sut = new LiveMigrationProgress
        {
            Iteration = 1,
            SourceVersion = 100,
            TargetVersion = 100,
            EventsCopiedThisIteration = 0,
            TotalEventsCopied = 100,
            ElapsedTime = TimeSpan.FromSeconds(10)
        };

        // Assert
        Assert.Equal(0, sut.EventsBehind);
    }

    [Fact]
    public void Should_return_zero_events_behind_when_target_ahead()
    {
        // This edge case shouldn't happen, but handle it gracefully
        var sut = new LiveMigrationProgress
        {
            Iteration = 1,
            SourceVersion = 95,
            TargetVersion = 100,
            EventsCopiedThisIteration = 0,
            TotalEventsCopied = 100,
            ElapsedTime = TimeSpan.FromSeconds(10)
        };

        // Assert - should not return negative
        Assert.Equal(0, sut.EventsBehind);
    }

    [Fact]
    public void Should_report_is_synced_when_versions_match()
    {
        // Arrange
        var sut = new LiveMigrationProgress
        {
            Iteration = 5,
            SourceVersion = 100,
            TargetVersion = 100,
            EventsCopiedThisIteration = 0,
            TotalEventsCopied = 100,
            ElapsedTime = TimeSpan.FromSeconds(5)
        };

        // Assert
        Assert.True(sut.IsSynced);
    }

    [Fact]
    public void Should_report_not_synced_when_behind()
    {
        // Arrange
        var sut = new LiveMigrationProgress
        {
            Iteration = 3,
            SourceVersion = 100,
            TargetVersion = 98,
            EventsCopiedThisIteration = 2,
            TotalEventsCopied = 98,
            ElapsedTime = TimeSpan.FromSeconds(3)
        };

        // Assert
        Assert.False(sut.IsSynced);
    }

    [Fact]
    public void Should_preserve_all_properties()
    {
        // Arrange
        var elapsed = TimeSpan.FromSeconds(42);

        var sut = new LiveMigrationProgress
        {
            Iteration = 7,
            SourceVersion = 500,
            TargetVersion = 450,
            EventsCopiedThisIteration = 25,
            TotalEventsCopied = 450,
            ElapsedTime = elapsed
        };

        // Assert
        Assert.Equal(7, sut.Iteration);
        Assert.Equal(500, sut.SourceVersion);
        Assert.Equal(450, sut.TargetVersion);
        Assert.Equal(25, sut.EventsCopiedThisIteration);
        Assert.Equal(450, sut.TotalEventsCopied);
        Assert.Equal(elapsed, sut.ElapsedTime);
        Assert.Equal(50, sut.EventsBehind);
        Assert.False(sut.IsSynced);
    }
}
