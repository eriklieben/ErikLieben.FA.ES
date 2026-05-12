using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.Postgres.HealthChecks;

/// <summary>
/// Health-check builder extensions for the Postgres provider.
/// </summary>
public static class HealthCheckBuilderExtensions
{
    /// <summary>Registers a <see cref="PostgresHealthCheck"/> with the supplied name, failure status, and tags.</summary>
    public static IHealthChecksBuilder AddPostgresHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "postgres",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddCheck<PostgresHealthCheck>(
            name,
            failureStatus,
            tags,
            timeout);
        return builder;
    }
}
