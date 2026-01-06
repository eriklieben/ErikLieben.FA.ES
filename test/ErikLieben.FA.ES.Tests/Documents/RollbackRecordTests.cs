using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Tests.Documents;

/// <summary>
/// Tests for the RollbackRecord class.
/// </summary>
public class RollbackRecordTests
{
    [Fact]
    public void RollbackRecord_should_have_default_values()
    {
        // Act
        var record = new RollbackRecord();

        // Assert
        Assert.Equal(default, record.RolledBackAt);
        Assert.Equal(0, record.FromVersion);
        Assert.Equal(0, record.ToVersion);
        Assert.Equal(0, record.EventsRemoved);
        Assert.Null(record.OriginalError);
        Assert.Null(record.OriginalExceptionType);
    }

    [Fact]
    public void RollbackRecord_should_store_all_properties()
    {
        // Arrange
        var rolledBackAt = DateTimeOffset.UtcNow;
        var fromVersion = 5;
        var toVersion = 10;
        var eventsRemoved = 4; // Only 4 of the 6 versions existed
        var originalError = "Service unavailable during commit";
        var exceptionType = "System.ServiceUnavailableException";

        // Act
        var record = new RollbackRecord
        {
            RolledBackAt = rolledBackAt,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            EventsRemoved = eventsRemoved,
            OriginalError = originalError,
            OriginalExceptionType = exceptionType
        };

        // Assert
        Assert.Equal(rolledBackAt, record.RolledBackAt);
        Assert.Equal(fromVersion, record.FromVersion);
        Assert.Equal(toVersion, record.ToVersion);
        Assert.Equal(eventsRemoved, record.EventsRemoved);
        Assert.Equal(originalError, record.OriginalError);
        Assert.Equal(exceptionType, record.OriginalExceptionType);
    }

    [Fact]
    public void RollbackRecord_should_allow_zero_events_removed()
    {
        // Arrange & Act - when no events were actually written before failure
        var record = new RollbackRecord
        {
            FromVersion = 5,
            ToVersion = 10,
            EventsRemoved = 0
        };

        // Assert
        Assert.Equal(0, record.EventsRemoved);
    }

    [Fact]
    public void RollbackRecord_events_removed_can_be_less_than_range()
    {
        // Arrange & Act - partial writes where some versions were never written
        var record = new RollbackRecord
        {
            FromVersion = 0,
            ToVersion = 9, // Range of 10
            EventsRemoved = 3 // Only 3 were actually written
        };

        // Assert - EventsRemoved can be less than (ToVersion - FromVersion + 1)
        var expectedRange = record.ToVersion - record.FromVersion + 1;
        Assert.True(record.EventsRemoved < expectedRange);
    }
}
