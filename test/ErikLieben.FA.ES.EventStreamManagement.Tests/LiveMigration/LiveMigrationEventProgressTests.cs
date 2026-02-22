using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.LiveMigration;

public class LiveMigrationEventProgressTests
{
    [Fact]
    public void Should_preserve_all_required_properties()
    {
        // Arrange
        var elapsed = TimeSpan.FromSeconds(42);

        var sut = new LiveMigrationEventProgress
        {
            Iteration = 3,
            EventVersion = 15,
            EventType = "WorkItem.Created",
            WasTransformed = false,
            TotalEventsCopied = 15,
            SourceVersion = 100,
            ElapsedTime = elapsed
        };

        // Assert
        Assert.Equal(3, sut.Iteration);
        Assert.Equal(15, sut.EventVersion);
        Assert.Equal("WorkItem.Created", sut.EventType);
        Assert.False(sut.WasTransformed);
        Assert.Equal(15, sut.TotalEventsCopied);
        Assert.Equal(100, sut.SourceVersion);
        Assert.Equal(elapsed, sut.ElapsedTime);
    }

    [Fact]
    public void Should_have_null_optional_properties_when_not_transformed()
    {
        // Arrange
        var sut = new LiveMigrationEventProgress
        {
            Iteration = 1,
            EventVersion = 5,
            EventType = "TestEvent",
            WasTransformed = false,
            TotalEventsCopied = 5,
            SourceVersion = 10,
            ElapsedTime = TimeSpan.FromSeconds(1)
        };

        // Assert
        Assert.Null(sut.OriginalEventType);
        Assert.Null(sut.OriginalSchemaVersion);
        Assert.Null(sut.NewSchemaVersion);
    }

    [Fact]
    public void Should_preserve_transformation_properties_when_transformed()
    {
        // Arrange
        var sut = new LiveMigrationEventProgress
        {
            Iteration = 2,
            EventVersion = 10,
            EventType = "WorkItem.CreatedV2",
            WasTransformed = true,
            OriginalEventType = "WorkItem.Created",
            OriginalSchemaVersion = 1,
            NewSchemaVersion = 2,
            TotalEventsCopied = 10,
            SourceVersion = 50,
            ElapsedTime = TimeSpan.FromSeconds(5)
        };

        // Assert
        Assert.True(sut.WasTransformed);
        Assert.Equal("WorkItem.Created", sut.OriginalEventType);
        Assert.Equal(1, sut.OriginalSchemaVersion);
        Assert.Equal(2, sut.NewSchemaVersion);
        Assert.Equal("WorkItem.CreatedV2", sut.EventType);
    }

    [Fact]
    public void Should_support_different_event_types()
    {
        // Arrange & Act
        var events = new[]
        {
            new LiveMigrationEventProgress
            {
                Iteration = 1,
                EventVersion = 1,
                EventType = "Project.Initiated",
                WasTransformed = false,
                TotalEventsCopied = 1,
                SourceVersion = 100,
                ElapsedTime = TimeSpan.Zero
            },
            new LiveMigrationEventProgress
            {
                Iteration = 1,
                EventVersion = 2,
                EventType = "WorkItem.Planned",
                WasTransformed = true,
                OriginalEventType = "WorkItem.Created",
                OriginalSchemaVersion = 1,
                NewSchemaVersion = 2,
                TotalEventsCopied = 2,
                SourceVersion = 100,
                ElapsedTime = TimeSpan.FromMilliseconds(100)
            },
            new LiveMigrationEventProgress
            {
                Iteration = 1,
                EventVersion = 3,
                EventType = "WorkItem.Completed",
                WasTransformed = false,
                TotalEventsCopied = 3,
                SourceVersion = 100,
                ElapsedTime = TimeSpan.FromMilliseconds(200)
            }
        };

        // Assert
        Assert.Equal(3, events.Length);
        Assert.Equal("Project.Initiated", events[0].EventType);
        Assert.True(events[1].WasTransformed);
        Assert.False(events[2].WasTransformed);
    }

    [Fact]
    public void Should_track_progress_through_total_events_copied()
    {
        // Arrange
        var progress = new List<LiveMigrationEventProgress>();

        // Simulate copying events
        for (int i = 1; i <= 5; i++)
        {
            progress.Add(new LiveMigrationEventProgress
            {
                Iteration = 1,
                EventVersion = i,
                EventType = $"Event{i}",
                WasTransformed = false,
                TotalEventsCopied = i,
                SourceVersion = 10,
                ElapsedTime = TimeSpan.FromMilliseconds(i * 100)
            });
        }

        // Assert
        Assert.Equal(5, progress.Count);
        Assert.Equal(1, progress[0].TotalEventsCopied);
        Assert.Equal(5, progress[4].TotalEventsCopied);

        // Verify monotonic increase
        for (int i = 1; i < progress.Count; i++)
        {
            Assert.True(progress[i].TotalEventsCopied > progress[i - 1].TotalEventsCopied);
        }
    }

    [Fact]
    public void Should_handle_schema_version_upgrade_scenarios()
    {
        // Arrange - v1 to v3 upgrade (skipping v2)
        var sut = new LiveMigrationEventProgress
        {
            Iteration = 1,
            EventVersion = 1,
            EventType = "WorkItem.CreatedV3",
            WasTransformed = true,
            OriginalEventType = "WorkItem.Created",
            OriginalSchemaVersion = 1,
            NewSchemaVersion = 3,
            TotalEventsCopied = 1,
            SourceVersion = 100,
            ElapsedTime = TimeSpan.FromSeconds(1)
        };

        // Assert
        Assert.True(sut.WasTransformed);
        Assert.Equal(1, sut.OriginalSchemaVersion);
        Assert.Equal(3, sut.NewSchemaVersion);
        Assert.Equal(2, sut.NewSchemaVersion - sut.OriginalSchemaVersion); // Jumped 2 versions
    }
}
