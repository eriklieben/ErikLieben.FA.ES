using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.LiveMigration;

public class LiveMigrationResultTests
{
    [Fact]
    public void Should_report_is_failure_when_not_successful()
    {
        // Arrange
        var sut = new LiveMigrationResult
        {
            Success = false,
            MigrationId = Guid.NewGuid(),
            SourceStreamId = "source",
            TargetStreamId = "target",
            TotalEventsCopied = 50,
            Iterations = 3,
            ElapsedTime = TimeSpan.FromSeconds(10),
            Error = "Test error"
        };

        // Assert
        Assert.True(sut.IsFailure);
        Assert.False(sut.Success);
    }

    [Fact]
    public void Should_report_not_failure_when_successful()
    {
        // Arrange
        var sut = new LiveMigrationResult
        {
            Success = true,
            MigrationId = Guid.NewGuid(),
            SourceStreamId = "source",
            TargetStreamId = "target",
            TotalEventsCopied = 100,
            Iterations = 5,
            ElapsedTime = TimeSpan.FromSeconds(15)
        };

        // Assert
        Assert.False(sut.IsFailure);
        Assert.True(sut.Success);
    }

    [Fact]
    public void Should_preserve_all_success_properties()
    {
        // Arrange
        var migrationId = Guid.NewGuid();
        var elapsed = TimeSpan.FromMinutes(2);

        var sut = new LiveMigrationResult
        {
            Success = true,
            MigrationId = migrationId,
            SourceStreamId = "orders-123",
            TargetStreamId = "orders-123-v2",
            TotalEventsCopied = 1500,
            Iterations = 12,
            ElapsedTime = elapsed
        };

        // Assert
        Assert.True(sut.Success);
        Assert.Equal(migrationId, sut.MigrationId);
        Assert.Equal("orders-123", sut.SourceStreamId);
        Assert.Equal("orders-123-v2", sut.TargetStreamId);
        Assert.Equal(1500, sut.TotalEventsCopied);
        Assert.Equal(12, sut.Iterations);
        Assert.Equal(elapsed, sut.ElapsedTime);
        Assert.Null(sut.Error);
        Assert.Null(sut.Exception);
    }

    [Fact]
    public void Should_preserve_all_failure_properties()
    {
        // Arrange
        var migrationId = Guid.NewGuid();
        var elapsed = TimeSpan.FromMinutes(5);
        var exception = new InvalidOperationException("Test exception");

        var sut = new LiveMigrationResult
        {
            Success = false,
            MigrationId = migrationId,
            SourceStreamId = "orders-456",
            TargetStreamId = "orders-456-v2",
            TotalEventsCopied = 750,
            Iterations = 100,
            ElapsedTime = elapsed,
            Error = "Close timeout exceeded",
            Exception = exception
        };

        // Assert
        Assert.False(sut.Success);
        Assert.Equal(migrationId, sut.MigrationId);
        Assert.Equal("orders-456", sut.SourceStreamId);
        Assert.Equal("orders-456-v2", sut.TargetStreamId);
        Assert.Equal(750, sut.TotalEventsCopied);
        Assert.Equal(100, sut.Iterations);
        Assert.Equal(elapsed, sut.ElapsedTime);
        Assert.Equal("Close timeout exceeded", sut.Error);
        Assert.Same(exception, sut.Exception);
    }
}
