using ErikLieben.FA.ES.Postgres;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Api.Services;

/// <summary>
/// Runs the Postgres provider's V1 schema install on startup so the event store tables
/// (faes_events, faes_documents, faes_snapshots, faes_projection_status,
/// faes_projection_checkpoints, …) exist before the API serves the first request.
/// Individual projection tables are created lazily by the factories themselves.
/// </summary>
public sealed class PostgresProjectionSchemaInitializer : IHostedService
{
    private readonly PostgresSchemaBootstrapper bootstrapper;
    private readonly ILogger<PostgresProjectionSchemaInitializer> logger;

    public PostgresProjectionSchemaInitializer(
        PostgresSchemaBootstrapper bootstrapper,
        ILogger<PostgresProjectionSchemaInitializer> logger)
    {
        this.bootstrapper = bootstrapper;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await bootstrapper.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Postgres schema bootstrap complete.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bootstrap Postgres schema.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
