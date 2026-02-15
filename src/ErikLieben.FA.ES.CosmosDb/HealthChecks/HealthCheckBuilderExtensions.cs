using ErikLieben.FA.ES.CosmosDb.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.CosmosDb.HealthChecks;

/// <summary>
/// Extension methods for adding Cosmos DB health checks.
/// </summary>
public static class HealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a health check for Azure Cosmos DB.
    /// </summary>
    /// <param name="builder">The health check builder.</param>
    /// <param name="name">The name of the health check. Defaults to "azure-cosmosdb".</param>
    /// <param name="failureStatus">The failure status. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="timeout">Optional timeout for the health check.</param>
    /// <returns>The health check builder.</returns>
    public static IHealthChecksBuilder AddCosmosDbHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "azure-cosmosdb",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp =>
            {
                var cosmosClient = sp.GetRequiredService<CosmosClient>();
                var settings = sp.GetRequiredService<EventStreamCosmosDbSettings>();
                return new CosmosDbHealthCheck(cosmosClient, settings);
            },
            failureStatus,
            tags,
            timeout));
    }
}
