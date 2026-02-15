using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.Tests.Projections;

public class RebuildInfoTests
{
    [Fact]
    public void Start_CreatesRebuildInfoWithCorrectProperties()
    {
        var before = DateTimeOffset.UtcNow;
        var info = RebuildInfo.Start(RebuildStrategy.BlueGreen, sourceVersion: 1, sourceCheckpointFingerprint: "abc123");
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(RebuildStrategy.BlueGreen, info.Strategy);
        Assert.Equal(1, info.SourceVersion);
        Assert.Equal("abc123", info.SourceCheckpointFingerprint);
        Assert.InRange(info.StartedAt, before, after);
        Assert.InRange(info.LastUpdatedAt, before, after);
        Assert.Null(info.CompletedAt);
        Assert.Null(info.Error);
    }

    [Fact]
    public void Start_DefaultValues_AreCorrect()
    {
        var info = RebuildInfo.Start(RebuildStrategy.BlockingWithCatchUp);

        Assert.Equal(RebuildStrategy.BlockingWithCatchUp, info.Strategy);
        Assert.Equal(0, info.SourceVersion);
        Assert.Null(info.SourceCheckpointFingerprint);
    }

    [Fact]
    public void IsInProgress_ReturnsTrue_WhenNotCompletedAndNoError()
    {
        var info = RebuildInfo.Start(RebuildStrategy.BlueGreen);

        Assert.True(info.IsInProgress);
        Assert.False(info.IsSuccessful);
        Assert.False(info.IsFailed);
    }

    [Fact]
    public async Task WithProgress_UpdatesLastUpdatedAt()
    {
        var info = RebuildInfo.Start(RebuildStrategy.BlueGreen);
        var originalTime = info.LastUpdatedAt;

        await Task.Delay(15);
        var updatedInfo = info.WithProgress();

        Assert.True(updatedInfo.LastUpdatedAt >= originalTime);
        Assert.Null(updatedInfo.CompletedAt);
        Assert.True(updatedInfo.IsInProgress);
    }

    [Fact]
    public void WithCompletion_SetsCompletedAt()
    {
        var info = RebuildInfo.Start(RebuildStrategy.BlueGreen);
        var completedInfo = info.WithCompletion();

        Assert.NotNull(completedInfo.CompletedAt);
        Assert.True(completedInfo.IsSuccessful);
        Assert.False(completedInfo.IsInProgress);
        Assert.False(completedInfo.IsFailed);
    }

    [Fact]
    public void WithError_SetsErrorAndIsFailed()
    {
        var info = RebuildInfo.Start(RebuildStrategy.BlueGreen);
        var failedInfo = info.WithError("Something went wrong");

        Assert.Equal("Something went wrong", failedInfo.Error);
        Assert.True(failedInfo.IsFailed);
        Assert.False(failedInfo.IsInProgress);
        Assert.False(failedInfo.IsSuccessful);
    }

    [Fact]
    public async Task Duration_ReturnsCorrectValue_WhenInProgress()
    {
        var info = RebuildInfo.Start(RebuildStrategy.BlueGreen);
        await Task.Delay(50);

        var duration = info.Duration;

        Assert.True(duration >= TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task Duration_ReturnsCorrectValue_WhenCompleted()
    {
        var info = RebuildInfo.Start(RebuildStrategy.BlueGreen);
        await Task.Delay(50);
        var completedInfo = info.WithCompletion();

        var duration = completedInfo.Duration;

        Assert.True(duration >= TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void RebuildStrategy_BlockingWithCatchUp_HasValue0()
    {
        Assert.Equal(0, (int)RebuildStrategy.BlockingWithCatchUp);
    }

    [Fact]
    public void RebuildStrategy_BlueGreen_HasValue1()
    {
        Assert.Equal(1, (int)RebuildStrategy.BlueGreen);
    }
}
