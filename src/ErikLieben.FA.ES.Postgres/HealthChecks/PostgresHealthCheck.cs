using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres.HealthChecks;

/// <summary>
/// Health check that issues <c>SELECT 1</c> against the configured Postgres data source.
/// </summary>
public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource dataSource;

    public PostgresHealthCheck(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand("SELECT 1", connection);
            await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Postgres reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres unreachable.", ex);
        }
    }
}
