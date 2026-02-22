using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.Tests.Projections;

public class ProjectionStatusCoordinatorTests
{
    private readonly InMemoryProjectionStatusCoordinator _coordinator;

    public ProjectionStatusCoordinatorTests()
    {
        _coordinator = new InMemoryProjectionStatusCoordinator();
    }

    [Fact]
    public async Task StartRebuildAsync_ReturnsValidToken()
    {
        var token = await _coordinator.StartRebuildAsync(
            "OrderDashboard",
            "order-123",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromHours(1));

        Assert.NotNull(token);
        Assert.Equal("OrderDashboard", token.ProjectionName);
        Assert.Equal("order-123", token.ObjectId);
        Assert.Equal(RebuildStrategy.BlueGreen, token.Strategy);
        Assert.False(token.IsExpired);
    }

    [Fact]
    public async Task StartRebuildAsync_SetsStatusToRebuilding()
    {
        var token = await _coordinator.StartRebuildAsync(
            "OrderDashboard",
            "order-123",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromHours(1));

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");

        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Rebuilding, status.Status);
        Assert.NotNull(status.RebuildInfo);
        Assert.Equal(RebuildStrategy.BlueGreen, status.RebuildInfo.Strategy);
    }

    [Fact]
    public async Task StartCatchUpAsync_SetsStatusToCatchingUp()
    {
        var token = await _coordinator.StartRebuildAsync(
            "OrderDashboard",
            "order-123",
            RebuildStrategy.BlockingWithCatchUp,
            TimeSpan.FromHours(1));

        await _coordinator.StartCatchUpAsync(token);

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");

        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.CatchingUp, status.Status);
    }

    [Fact]
    public async Task MarkReadyAsync_SetsStatusToReady()
    {
        var token = await _coordinator.StartRebuildAsync(
            "OrderDashboard",
            "order-123",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromHours(1));

        await _coordinator.MarkReadyAsync(token);

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");

        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Ready, status.Status);
        Assert.NotNull(status.RebuildInfo?.CompletedAt);
    }

    [Fact]
    public async Task CompleteRebuildAsync_SetsStatusToActive()
    {
        var token = await _coordinator.StartRebuildAsync(
            "OrderDashboard",
            "order-123",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromHours(1));

        await _coordinator.CompleteRebuildAsync(token);

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");

        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Active, status.Status);
    }

    [Fact]
    public async Task CancelRebuildAsync_WithError_SetsStatusToFailed()
    {
        var token = await _coordinator.StartRebuildAsync(
            "OrderDashboard",
            "order-123",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromHours(1));

        await _coordinator.CancelRebuildAsync(token, "Something went wrong");

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");

        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Failed, status.Status);
        Assert.Equal("Something went wrong", status.RebuildInfo?.Error);
    }

    [Fact]
    public async Task CancelRebuildAsync_WithoutError_SetsStatusToActive()
    {
        var token = await _coordinator.StartRebuildAsync(
            "OrderDashboard",
            "order-123",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromHours(1));

        await _coordinator.CancelRebuildAsync(token);

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");

        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Active, status.Status);
    }

    [Fact]
    public async Task GetByStatusAsync_ReturnsCorrectProjections()
    {
        await _coordinator.StartRebuildAsync("Projection1", "obj-1", RebuildStrategy.BlueGreen, TimeSpan.FromHours(1));
        await _coordinator.StartRebuildAsync("Projection2", "obj-2", RebuildStrategy.BlueGreen, TimeSpan.FromHours(1));
        await _coordinator.DisableAsync("Projection3", "obj-3");

        var rebuilding = await _coordinator.GetByStatusAsync(ProjectionStatus.Rebuilding);
        var disabled = await _coordinator.GetByStatusAsync(ProjectionStatus.Disabled);

        Assert.Equal(2, rebuilding.Count());
        Assert.Single(disabled);
    }

    [Fact]
    public async Task DisableAsync_SetsStatusToDisabled()
    {
        await _coordinator.DisableAsync("OrderDashboard", "order-123");

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");

        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Disabled, status.Status);
    }

    [Fact]
    public async Task EnableAsync_SetsStatusToActive()
    {
        await _coordinator.DisableAsync("OrderDashboard", "order-123");
        await _coordinator.EnableAsync("OrderDashboard", "order-123");

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");

        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Active, status.Status);
    }

    [Fact]
    public async Task RecoverStuckRebuildsAsync_RecoversExpiredRebuilds()
    {
        // Start a rebuild with a very short timeout
        var token = await _coordinator.StartRebuildAsync(
            "OrderDashboard",
            "order-123",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromMilliseconds(1));

        // Wait for it to expire
        await Task.Delay(10);

        var recovered = await _coordinator.RecoverStuckRebuildsAsync();

        Assert.Equal(1, recovered);

        var status = await _coordinator.GetStatusAsync("OrderDashboard", "order-123");
        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Failed, status.Status);
        Assert.Contains("timed out", status.RebuildInfo?.Error);
    }

    [Fact]
    public async Task CompleteRebuildAsync_WithInvalidToken_ThrowsException()
    {
        var fakeToken = RebuildToken.Create("OrderDashboard", "order-123", RebuildStrategy.BlueGreen, TimeSpan.FromHours(1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _coordinator.CompleteRebuildAsync(fakeToken));
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNull_WhenNotFound()
    {
        var status = await _coordinator.GetStatusAsync("NonExistent", "obj-1");

        Assert.Null(status);
    }

    [Fact]
    public void RebuildToken_Create_GeneratesUniqueTokens()
    {
        var token1 = RebuildToken.Create("Projection", "obj-1", RebuildStrategy.BlueGreen, TimeSpan.FromHours(1));
        var token2 = RebuildToken.Create("Projection", "obj-1", RebuildStrategy.BlueGreen, TimeSpan.FromHours(1));

        Assert.NotEqual(token1.Token, token2.Token);
    }

    [Fact]
    public void RebuildToken_IsExpired_ReturnsTrue_WhenExpired()
    {
        var token = new RebuildToken(
            "Projection",
            "obj-1",
            "token",
            RebuildStrategy.BlueGreen,
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(-1));

        Assert.True(token.IsExpired);
    }
}
