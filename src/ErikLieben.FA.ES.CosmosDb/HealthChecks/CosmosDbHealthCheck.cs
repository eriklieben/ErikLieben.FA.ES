using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Observability;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.CosmosDb.HealthChecks;

/// <summary>
/// Health check for Azure Cosmos DB connectivity.
/// </summary>
/// <remarks>
/// <para>
/// This health check verifies that the configured Azure Cosmos DB account is accessible
/// and responsive. It performs a lightweight operation (read account) to verify connectivity.
/// </para>
/// <para>
/// The check uses OpenTelemetry tracing for observability.
/// </para>
/// </remarks>
public class CosmosDbHealthCheck : IHealthCheck
{
    private readonly CosmosClient _cosmosClient;
    private readonly EventStreamCosmosDbSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbHealthCheck"/> class.
    /// </summary>
    /// <param name="cosmosClient">The Cosmos DB client.</param>
    /// <param name="settings">The Cosmos DB settings.</param>
    public CosmosDbHealthCheck(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(settings);

        _cosmosClient = cosmosClient;
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("CosmosDbHealthCheck");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemCosmosDb);
            activity.SetTag(FaesSemanticConventions.StorageProvider, FaesSemanticConventions.StorageProviderCosmosDb);
            activity.SetTag(FaesSemanticConventions.DbName, _settings.DatabaseName);
        }

        try
        {
            // Perform a lightweight operation to verify connectivity
            var response = await _cosmosClient.ReadAccountAsync();

            var data = new Dictionary<string, object>
            {
                { "DatabaseId", _settings.DatabaseName },
                { "AccountId", response.Id },
                { "ConsistencyLevel", response.Consistency.DefaultConsistencyLevel.ToString() }
            };

            // Add readable regions count if available
            if (response.ReadableRegions != null)
            {
                data["ReadableRegions"] = response.ReadableRegions.Count();
            }

            activity?.SetTag(FaesSemanticConventions.Success, true);

            return HealthCheckResult.Healthy(
                description: "Azure Cosmos DB is accessible",
                data: data);
        }
        catch (Exception ex)
        {
            FaesInstrumentation.RecordException(activity, ex);
            activity?.SetTag(FaesSemanticConventions.Success, false);

            return HealthCheckResult.Unhealthy(
                description: $"Azure Cosmos DB is not accessible: {ex.Message}",
                exception: ex);
        }
    }
}
