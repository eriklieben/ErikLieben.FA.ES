using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Snapshots;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Snapshots;

public class SnapshotCleanupServiceTests
{
    private readonly ISnapShotStore _snapshotStore;
    private readonly ISnapshotPolicyProvider _policyProvider;
    private readonly SnapshotCleanupService _service;

    public SnapshotCleanupServiceTests()
    {
        _snapshotStore = Substitute.For<ISnapShotStore>();
        _policyProvider = Substitute.For<ISnapshotPolicyProvider>();
        _service = new SnapshotCleanupService(_snapshotStore, _policyProvider);
    }

    [Fact]
    public async Task CleanupAsync_ReturnsNoPolicyConfigured_WhenNoPolicy()
    {
        var document = CreateMockDocument("stream-1");
        _policyProvider.GetPolicy(Arg.Any<Type>()).Returns((SnapshotPolicy?)null);

        var result = await _service.CleanupAsync(document, typeof(TestAggregate));

        Assert.Equal(0, result.SnapshotsDeleted);
        Assert.Equal("No cleanup policy configured", result.Reason);
    }

    [Fact]
    public async Task CleanupAsync_ReturnsNoPolicyConfigured_WhenPolicyDisabled()
    {
        var document = CreateMockDocument("stream-1");
        _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(new SnapshotPolicy { Enabled = false });

        var result = await _service.CleanupAsync(document, typeof(TestAggregate));

        Assert.Equal(0, result.SnapshotsDeleted);
        Assert.Equal("No cleanup policy configured", result.Reason);
    }

    [Fact]
    public async Task CleanupAsync_ReturnsNoCleanupNeeded_WhenNoSnapshots()
    {
        var document = CreateMockDocument("stream-1");
        var policy = new SnapshotPolicy { Enabled = true, KeepSnapshots = 3 };

        _snapshotStore.ListSnapshotsAsync(document, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _service.CleanupAsync(document, policy);

        Assert.Equal(0, result.SnapshotsDeleted);
        Assert.Equal(0, result.SnapshotsRetained);
    }

    [Fact]
    public async Task CleanupAsync_DeletesOldSnapshots_WhenExceedsKeepLimit()
    {
        var document = CreateMockDocument("stream-1");
        var policy = new SnapshotPolicy { Enabled = true, KeepSnapshots = 2 };
        var now = DateTimeOffset.UtcNow;

        var snapshots = new List<SnapshotMetadata>
        {
            new(100, now.AddMinutes(-1)),    // Most recent - keep
            new(75, now.AddMinutes(-10)),    // Keep
            new(50, now.AddMinutes(-20)),    // Delete
            new(25, now.AddMinutes(-30)),    // Delete
        };

        _snapshotStore.ListSnapshotsAsync(document, Arg.Any<CancellationToken>())
            .Returns(snapshots);
        _snapshotStore.DeleteManyAsync(document, Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(2);

        var result = await _service.CleanupAsync(document, policy);

        Assert.Equal(2, result.SnapshotsDeleted);
        Assert.Equal(2, result.SnapshotsRetained);
        Assert.Contains(50, result.DeletedVersions);
        Assert.Contains(25, result.DeletedVersions);
    }

    [Fact]
    public async Task CleanupAsync_DeletesOldSnapshots_WhenExceedsMaxAge()
    {
        var document = CreateMockDocument("stream-1");
        var policy = new SnapshotPolicy
        {
            Enabled = true,
            KeepSnapshots = 10, // Higher than count to test age limit
            MaxAge = TimeSpan.FromDays(7)
        };
        var now = DateTimeOffset.UtcNow;

        var snapshots = new List<SnapshotMetadata>
        {
            new(100, now.AddDays(-1)),   // Keep (recent)
            new(75, now.AddDays(-5)),    // Keep (within age)
            new(50, now.AddDays(-10)),   // Delete (too old)
            new(25, now.AddDays(-30)),   // Delete (too old)
        };

        _snapshotStore.ListSnapshotsAsync(document, Arg.Any<CancellationToken>())
            .Returns(snapshots);
        _snapshotStore.DeleteManyAsync(document, Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(2);

        var result = await _service.CleanupAsync(document, policy);

        Assert.Equal(2, result.SnapshotsDeleted);
        Assert.Contains(50, result.DeletedVersions);
        Assert.Contains(25, result.DeletedVersions);
    }

    [Fact]
    public async Task CleanupAsync_AlwaysKeepsMostRecentSnapshot()
    {
        var document = CreateMockDocument("stream-1");
        var policy = new SnapshotPolicy
        {
            Enabled = true,
            KeepSnapshots = 0, // Would delete all
            MaxAge = TimeSpan.FromDays(1)
        };
        var now = DateTimeOffset.UtcNow;

        var snapshots = new List<SnapshotMetadata>
        {
            new(100, now.AddDays(-30)), // Old but most recent - KEEP
        };

        _snapshotStore.ListSnapshotsAsync(document, Arg.Any<CancellationToken>())
            .Returns(snapshots);

        var result = await _service.CleanupAsync(document, policy);

        Assert.Equal(0, result.SnapshotsDeleted);
        Assert.Equal(1, result.SnapshotsRetained);
        await _snapshotStore.DidNotReceive()
            .DeleteManyAsync(Arg.Any<IObjectDocument>(), Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsync_NoCleanup_WhenBelowRetentionLimits()
    {
        var document = CreateMockDocument("stream-1");
        var policy = new SnapshotPolicy { Enabled = true, KeepSnapshots = 5 };
        var now = DateTimeOffset.UtcNow;

        var snapshots = new List<SnapshotMetadata>
        {
            new(100, now.AddMinutes(-1)),
            new(75, now.AddMinutes(-10)),
            new(50, now.AddMinutes(-20)),
        };

        _snapshotStore.ListSnapshotsAsync(document, Arg.Any<CancellationToken>())
            .Returns(snapshots);

        var result = await _service.CleanupAsync(document, policy);

        Assert.Equal(0, result.SnapshotsDeleted);
        Assert.Equal(3, result.SnapshotsRetained);
        Assert.Equal("Retention limits not exceeded", result.Reason);
    }

    private static IObjectDocument CreateMockDocument(string streamId)
    {
        var document = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation { StreamIdentifier = streamId };
        document.Active.Returns(streamInfo);
        document.ObjectName.Returns("test");
        return document;
    }

    private class TestAggregate { }
}
