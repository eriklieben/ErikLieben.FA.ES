using ErikLieben.FA.ES.EventStreamManagement.Events;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Events;

public class StreamClosedEventTests
{
    [Fact]
    public void Should_have_correct_event_type_name_constant()
    {
        // Assert
        Assert.Equal("EventStream.Closed", StreamClosedEvent.EventTypeName);
    }

    [Fact]
    public void Should_require_continuation_stream_id()
    {
        // Act
        var sut = new StreamClosedEvent
        {
            ContinuationStreamId = "target-stream",
            ContinuationStreamType = "blob",
            ContinuationDataStore = "default",
            ContinuationDocumentStore = "default",
            Reason = StreamClosureReason.Migration,
            ClosedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.Equal("target-stream", sut.ContinuationStreamId);
    }

    [Fact]
    public void Should_preserve_all_required_properties()
    {
        // Arrange
        var closedAt = DateTimeOffset.UtcNow;

        // Act
        var sut = new StreamClosedEvent
        {
            ContinuationStreamId = "orders-123-v2",
            ContinuationStreamType = "cosmos",
            ContinuationDataStore = "cosmosdb-prod",
            ContinuationDocumentStore = "cosmosdb-prod-docs",
            Reason = StreamClosureReason.Migration,
            ClosedAt = closedAt
        };

        // Assert
        Assert.Equal("orders-123-v2", sut.ContinuationStreamId);
        Assert.Equal("cosmos", sut.ContinuationStreamType);
        Assert.Equal("cosmosdb-prod", sut.ContinuationDataStore);
        Assert.Equal("cosmosdb-prod-docs", sut.ContinuationDocumentStore);
        Assert.Equal(StreamClosureReason.Migration, sut.Reason);
        Assert.Equal(closedAt, sut.ClosedAt);
    }

    [Fact]
    public void Should_support_optional_properties()
    {
        // Act
        var sut = new StreamClosedEvent
        {
            ContinuationStreamId = "target-stream",
            ContinuationStreamType = "blob",
            ContinuationDataStore = "default",
            ContinuationDocumentStore = "default",
            Reason = StreamClosureReason.Migration,
            ClosedAt = DateTimeOffset.UtcNow,
            MigrationId = "migration-abc-123",
            LastBusinessEventVersion = 100,
            Metadata = new Dictionary<string, string>
            {
                ["operator"] = "admin",
                ["reason_detail"] = "Schema upgrade"
            }
        };

        // Assert
        Assert.Equal("migration-abc-123", sut.MigrationId);
        Assert.Equal(100, sut.LastBusinessEventVersion);
        Assert.NotNull(sut.Metadata);
        Assert.Equal("admin", sut.Metadata["operator"]);
    }

    [Theory]
    [InlineData(StreamClosureReason.Migration)]
    [InlineData(StreamClosureReason.SizeLimit)]
    [InlineData(StreamClosureReason.Archival)]
    [InlineData(StreamClosureReason.Manual)]
    public void Should_support_all_closure_reasons(StreamClosureReason reason)
    {
        // Act
        var sut = new StreamClosedEvent
        {
            ContinuationStreamId = "target",
            ContinuationStreamType = "blob",
            ContinuationDataStore = "default",
            ContinuationDocumentStore = "default",
            Reason = reason,
            ClosedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.Equal(reason, sut.Reason);
    }

    [Fact]
    public void Should_default_optional_properties_to_null_or_default()
    {
        // Act
        var sut = new StreamClosedEvent
        {
            ContinuationStreamId = "target",
            ContinuationStreamType = "blob",
            ContinuationDataStore = "default",
            ContinuationDocumentStore = "default",
            Reason = StreamClosureReason.Migration,
            ClosedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.Null(sut.MigrationId);
        Assert.Null(sut.Metadata);
        Assert.Equal(0, sut.LastBusinessEventVersion);
    }
}

public class StreamClosureReasonTests
{
    [Fact]
    public void Should_have_migration_as_zero()
    {
        // Migration should be the default (0) value
        Assert.Equal(0, (int)StreamClosureReason.Migration);
    }

    [Fact]
    public void Should_have_distinct_values()
    {
        var values = Enum.GetValues<StreamClosureReason>();
        var distinct = values.Distinct().ToList();

        Assert.Equal(values.Length, distinct.Count);
    }

    [Fact]
    public void Should_have_four_reasons()
    {
        var values = Enum.GetValues<StreamClosureReason>();
        Assert.Equal(4, values.Length);
    }
}
