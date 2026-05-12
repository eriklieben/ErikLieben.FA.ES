using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresProjectionStatusCoordinatorTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private PostgresProjectionStatusCoordinator coordinator = null!;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        coordinator = new PostgresProjectionStatusCoordinator(fixture.DataSource);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Start_then_complete_rebuild_returns_to_Active()
    {
        var token = await coordinator.StartRebuildAsync(
            "OrderProjection", "order-1",
            RebuildStrategy.BlockingWithCatchUp,
            TimeSpan.FromMinutes(5));

        var status = await coordinator.GetStatusAsync("OrderProjection", "order-1");
        Assert.NotNull(status);
        Assert.Equal(ProjectionStatus.Rebuilding, status!.Status);
        Assert.NotNull(status.RebuildInfo);

        await coordinator.CompleteRebuildAsync(token);

        var afterStatus = await coordinator.GetStatusAsync("OrderProjection", "order-1");
        Assert.Equal(ProjectionStatus.Active, afterStatus!.Status);
        Assert.NotNull(afterStatus.RebuildInfo);
        Assert.NotNull(afterStatus.RebuildInfo!.CompletedAt);
    }

    [Fact]
    public async Task Cancel_rebuild_with_error_marks_failed()
    {
        var token = await coordinator.StartRebuildAsync(
            "OrderProjection", "order-2",
            RebuildStrategy.BlockingWithCatchUp,
            TimeSpan.FromMinutes(5));

        await coordinator.CancelRebuildAsync(token, error: "boom");

        var status = await coordinator.GetStatusAsync("OrderProjection", "order-2");
        Assert.Equal(ProjectionStatus.Failed, status!.Status);
        Assert.Equal("boom", status.RebuildInfo!.Error);
    }

    [Fact]
    public async Task BlueGreen_lifecycle_progresses_through_Ready_then_Active()
    {
        var token = await coordinator.StartRebuildAsync(
            "OrderProjection", "order-3",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromMinutes(5));

        await coordinator.MarkReadyAsync(token);
        var ready = await coordinator.GetStatusAsync("OrderProjection", "order-3");
        Assert.Equal(ProjectionStatus.Ready, ready!.Status);

        await coordinator.CompleteRebuildAsync(token);
        var active = await coordinator.GetStatusAsync("OrderProjection", "order-3");
        Assert.Equal(ProjectionStatus.Active, active!.Status);
    }

    [Fact]
    public async Task Blocking_lifecycle_passes_through_CatchingUp()
    {
        var token = await coordinator.StartRebuildAsync(
            "OrderProjection", "order-4",
            RebuildStrategy.BlockingWithCatchUp,
            TimeSpan.FromMinutes(5));

        await coordinator.StartCatchUpAsync(token);
        var catching = await coordinator.GetStatusAsync("OrderProjection", "order-4");
        Assert.Equal(ProjectionStatus.CatchingUp, catching!.Status);

        await coordinator.CompleteRebuildAsync(token);
        Assert.Equal(ProjectionStatus.Active, (await coordinator.GetStatusAsync("OrderProjection", "order-4"))!.Status);
    }

    [Fact]
    public async Task Invalid_token_rejected()
    {
        await coordinator.StartRebuildAsync(
            "OrderProjection", "order-5",
            RebuildStrategy.BlockingWithCatchUp,
            TimeSpan.FromMinutes(5));

        var fake = RebuildToken.Create("OrderProjection", "order-5", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartCatchUpAsync(fake));
    }

    [Fact]
    public async Task RecoverStuckRebuilds_marks_expired_tokens_as_Failed()
    {
        // Start with a 1 ms timeout so the token is already expired by the time we recover.
        var token = await coordinator.StartRebuildAsync(
            "OrderProjection", "order-6",
            RebuildStrategy.BlockingWithCatchUp,
            TimeSpan.FromMilliseconds(1));
        _ = token;

        await Task.Delay(50);

        var recovered = await coordinator.RecoverStuckRebuildsAsync();
        Assert.Equal(1, recovered);

        var status = await coordinator.GetStatusAsync("OrderProjection", "order-6");
        Assert.Equal(ProjectionStatus.Failed, status!.Status);
        Assert.NotNull(status.RebuildInfo!.Error);
    }

    [Fact]
    public async Task Disable_and_Enable_cycle()
    {
        await coordinator.DisableAsync("OrderProjection", "order-7");
        var disabled = await coordinator.GetStatusAsync("OrderProjection", "order-7");
        Assert.Equal(ProjectionStatus.Disabled, disabled!.Status);

        await coordinator.EnableAsync("OrderProjection", "order-7");
        var enabled = await coordinator.GetStatusAsync("OrderProjection", "order-7");
        Assert.Equal(ProjectionStatus.Active, enabled!.Status);
    }

    [Fact]
    public async Task GetByStatus_returns_matching_rows()
    {
        await coordinator.StartRebuildAsync("P", "a", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));
        await coordinator.StartRebuildAsync("P", "b", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(5));
        await coordinator.DisableAsync("P", "c");

        var rebuilding = (await coordinator.GetByStatusAsync(ProjectionStatus.Rebuilding)).ToList();
        Assert.Equal(2, rebuilding.Count);

        var disabled = (await coordinator.GetByStatusAsync(ProjectionStatus.Disabled)).ToList();
        Assert.Single(disabled);
        Assert.Equal("c", disabled[0].ObjectId);
    }
}
