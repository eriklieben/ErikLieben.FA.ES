using ErikLieben.FA.ES.Snapshots;

namespace ErikLieben.FA.ES.Tests.Snapshots;

public class SnapshotMetadataTests
{
    [Fact]
    public void Age_ReturnsCorrectDuration()
    {
        var createdAt = DateTimeOffset.UtcNow.AddHours(-2);
        var metadata = new SnapshotMetadata(100, createdAt);

        var age = metadata.Age;

        Assert.True(age >= TimeSpan.FromHours(2) && age < TimeSpan.FromHours(2.1));
    }

    [Fact]
    public void IsOlderThan_ReturnsTrueWhenOlder()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-10);
        var metadata = new SnapshotMetadata(100, createdAt);

        Assert.True(metadata.IsOlderThan(TimeSpan.FromDays(7)));
    }

    [Fact]
    public void IsOlderThan_ReturnsFalseWhenNewer()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-3);
        var metadata = new SnapshotMetadata(100, createdAt);

        Assert.False(metadata.IsOlderThan(TimeSpan.FromDays(7)));
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var metadata = new SnapshotMetadata(100, createdAt, "variant", 1024);

        Assert.Equal(100, metadata.Version);
        Assert.Equal(createdAt, metadata.CreatedAt);
        Assert.Equal("variant", metadata.Name);
        Assert.Equal(1024, metadata.SizeBytes);
    }
}
